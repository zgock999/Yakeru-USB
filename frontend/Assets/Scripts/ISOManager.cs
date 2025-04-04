using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace YakeruUSB
{
    public class ISOManager : MonoBehaviour
    {
        [SerializeField] private float refreshInterval = 5f;

        private List<ISOFile> isoFiles = new List<ISOFile>();
        private List<USBDevice> usbDevices = new List<USBDevice>();
        
        public ISOFile SelectedISO { get; private set; }
        public USBDevice SelectedDevice { get; private set; }
        
        private bool isWriting = false;
        public bool IsWriting => isWriting;

        // イベント定義
        public event Action<List<ISOFile>> OnISOFilesUpdated;
        public event Action<List<USBDevice>> OnUSBDevicesUpdated;
        public event Action<ProgressData> OnWriteProgressUpdated;
        public event Action OnWriteStarted;
        public event Action OnWriteCompleted;
        public event Action<string> OnWriteError;

        // シングルトンインスタンス
        private static ISOManager _instance;
        private static bool ApplicationIsQuitting = false;
        public static ISOManager Instance
        {
            get
            {
                // アプリケーション終了中は新しいインスタンスを作成しない
                if (ApplicationIsQuitting)
                {
                    return null;
                }
                
                if (_instance == null)
                {
                    #if UNITY_2022_3_OR_NEWER
                    _instance = FindAnyObjectByType<ISOManager>();
                    #else
                    _instance = FindAnyObjectByType<ISOManager>();
                    #endif
                    
                    if (_instance == null && !ApplicationIsQuitting)
                    {
                        GameObject go = new GameObject("ISOManager");
                        _instance = go.AddComponent<ISOManager>();
                        
                        // プレイモードでのみDontDestroyOnLoadを呼び出す
                        if (Application.isPlaying)
                        {
                            DontDestroyOnLoad(go);
                        }
                    }
                }
                return _instance;
            }
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            
            // プレイモードでのみDontDestroyOnLoadを呼び出す
            if (Application.isPlaying)
            {
                DontDestroyOnLoad(gameObject);
            }
        }

        private void OnApplicationQuit()
        {
            ApplicationIsQuitting = true;
            _instance = null;
            
            // WebSocketクライアントとの接続を切断
            if (WebSocketClient.Instance != null)
            {
                WebSocketClient.Instance.Disconnect();
            }
        }

        private void OnDestroy()
        {
            // このオブジェクトが破棄されるときにイベントリスナーを解除
            if (Application.isPlaying && WebSocketClient.Instance != null)
            {
                WebSocketClient.Instance.OnProgressUpdated -= HandleProgressUpdate;
            }
            
            // このインスタンスが現在のシングルトンインスタンスであれば、参照をクリア
            if (_instance == this)
            {
                _instance = null;
            }
        }

        private void Start()
        {
            // 起動時の状態を完全にリセット
            ResetAllState();
            
            // WebSocketのイベントを購読
            WebSocketClient.Instance.OnProgressUpdated += HandleProgressUpdate;
            
            // WebSocketの接続前に状態をクリアするように変更
            WebSocketClient.Instance.ResetConnectionState();
            WebSocketClient.Instance.Connect();
            
            // 定期的に一覧を更新するコルーチンを開始
            StartRefreshRoutine();
        }

        // 定期更新コルーチンを開始するメソッドを追加
        private void StartRefreshRoutine()
        {
            // 既存のコルーチンを停止
            if (_refreshCoroutine != null)
            {
                StopCoroutine(_refreshCoroutine);
                _refreshCoroutine = null;
            }
            
            // 定期更新を開始
            _refreshCoroutine = StartCoroutine(RefreshDataRoutine());
        }

        // 定期更新コルーチンを停止するメソッドを追加
        private void StopRefreshRoutine()
        {
            if (_refreshCoroutine != null)
            {
                StopCoroutine(_refreshCoroutine);
                _refreshCoroutine = null;
                Debug.Log("Stopped periodic refresh routine during write operation");
            }
        }

        // 定期更新コルーチンの参照を保持する変数
        private Coroutine _refreshCoroutine;

        private IEnumerator RefreshDataRoutine()
        {
            while (true)
            {
                if (!isWriting) // 書き込み中でない場合のみ更新
                {
                    RefreshISOFiles();
                    RefreshUSBDevices();
                }
                
                yield return new WaitForSeconds(refreshInterval);
            }
        }

        // ISOファイル一覧を更新
        public void RefreshISOFiles()
        {
            StartCoroutine(APIClient.Instance.GetISOFiles(
                onSuccess: files =>
                {
                    isoFiles = files;
                    OnISOFilesUpdated?.Invoke(isoFiles);
                    
                    // 選択中のISOファイルが一覧から削除された場合は選択解除
                    if (SelectedISO != null && !isoFiles.Exists(iso => iso.name == SelectedISO.name))
                    {
                        SelectedISO = null;
                    }
                },
                onError: error =>
                {
                    Debug.LogError($"Failed to get ISO files: {error}");
                }
            ));
        }

        // USBデバイス一覧を更新（Linuxでは強制再スキャンを試行）
        public void RefreshUSBDevices()
        {
            // 書き込み中はデバイス更新を抑制 - クライアント側でもチェック
            if (isWriting)
            {
                Debug.Log("USB device refresh suppressed during active write operation (client-side check)");
                // 書き込み中はUSBデバイス更新イベントを発火せず、空リストを返すわけでもない
                return;
            }

            // プラットフォームに応じた処理
            if (Application.platform == RuntimePlatform.LinuxEditor || 
                Application.platform == RuntimePlatform.LinuxPlayer)
            {
                // Linux環境では強制的な再スキャンを試行
                StartCoroutine(APIClient.Instance.RescanUSBDevices(
                    onSuccess: devices =>
                    {
                        // バックエンドが書き込み中で空リストを返した場合のチェック
                        if (devices.Count == 0 && isWriting)
                        {
                            Debug.Log("Backend blocked USB device scan due to active write - no device updates performed");
                            return;
                        }

                        usbDevices = devices;
                        OnUSBDevicesUpdated?.Invoke(usbDevices);
                        
                        // 選択中のUSBデバイスが一覧から削除された場合は選択解除
                        if (SelectedDevice != null && !usbDevices.Exists(dev => dev.id == SelectedDevice.id))
                        {
                            SelectedDevice = null;
                        }
                    },
                    onError: error =>
                    {
                        Debug.LogError($"Failed to rescan USB devices: {error}");
                        // 通常のAPIコールにフォールバック
                        FallbackRefreshUSBDevices();
                    }
                ));
            }
            else
            {
                // 通常の更新処理
                FallbackRefreshUSBDevices();
            }
        }
        
        // 通常のUSBデバイス更新処理（フォールバック用）
        private void FallbackRefreshUSBDevices()
        {
            // 書き込み中はデバイス更新を抑制（バックアップチェック）
            if (isWriting)
            {
                Debug.Log("USB device refresh (fallback) suppressed during active write operation");
                return;
            }

            StartCoroutine(APIClient.Instance.GetUSBDevices(
                onSuccess: devices =>
                {
                    // バックエンド側のブロックチェック
                    if (devices.Count == 0)
                    {
                        // バックエンドからデバイスが0件の場合、別途ログ出力
                        // これはバックエンドの書き込み中フラグによるブロックの可能性も含む
                        Debug.Log("No USB devices found or backend blocked device scan - no device updates performed");
                        return;
                    }
                    
                    usbDevices = devices;
                    OnUSBDevicesUpdated?.Invoke(usbDevices);
                    
                    // 選択中のUSBデバイスが一覧から削除された場合は選択解除
                    if (SelectedDevice != null && !usbDevices.Exists(dev => dev.id == SelectedDevice.id))
                    {
                        SelectedDevice = null;
                    }
                },
                onError: error =>
                {
                    Debug.LogError($"Failed to get USB devices: {error}");
                }
            ));
        }

        // ISOファイルを選択
        public void SelectISO(ISOFile isoFile)
        {
            SelectedISO = isoFile;
        }

        // USBデバイスを選択
        public void SelectDevice(USBDevice device)
        {
            SelectedDevice = device;
        }
        
        // 選択されたISOをクリアするメソッドを追加
        public void ClearSelectedISO()
        {
            SelectedISO = null;
        }

        // 書き込み開始
        public void StartWriting()
        {
            if (SelectedISO == null || SelectedDevice == null)
            {
                OnWriteError?.Invoke("ISO file or USB device not selected");
                return;
            }

            if (isWriting)
            {
                OnWriteError?.Invoke("Writing is already in progress");
                return;
            }

            // 書き込み開始前に定期更新を停止
            StopRefreshRoutine();

            // 書き込みフラグを先に設定し、OnWriteStartedを発火
            // これにより「書き込み開始中」の状態をUIに伝える
            isWriting = true;
            OnWriteStarted?.Invoke();
            
            // WebSocket接続を開始または再接続
            WebSocketClient.Instance.ResetConnectionState();
            WebSocketClient.Instance.Connect();

            // 書き込み開始前に、メディアのアンマウントを保証するオプションを追加
            StartCoroutine(PrepareDeviceAndWrite());
        }

        private IEnumerator PrepareDeviceAndWrite()
        {
            // デバイス準備に関連する変数
            bool devicePrepared = false;
            bool preparationError = false;
            string errorMessage = "";
            
            #region デバイスの準備
            // Linux環境の場合のみ、特別な準備処理を追加
            if (Application.platform == RuntimePlatform.LinuxEditor || 
                Application.platform == RuntimePlatform.LinuxPlayer)
            {
                // 準備APIが実装されている場合はそれを呼び出す
                if (APIClient.Instance.HasPrepareDeviceAPI())
                {
                    Debug.Log("Attempting to prepare device before writing...");
                    
                    // コルーチンの呼び出しをtry-catchブロック外で行う
                    bool prepareSuccess = false;
                    string prepareError = null;
                    
                    // PrepareDeviceの呼び出しはtry-catchの外で行う
                    yield return StartCoroutine(
                        PrepareDeviceWithCallback(
                            SelectedDevice.id,
                            (success) => { prepareSuccess = success; },
                            (error) => { prepareError = error; }
                        )
                    );
                    
                    // 結果の処理はtry-catchで行う
                    try
                    {
                        devicePrepared = prepareSuccess;
                        if (!prepareSuccess && !string.IsNullOrEmpty(prepareError))
                        {
                            Debug.LogWarning($"Device preparation warning: {prepareError}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"Error during device preparation: {ex.Message}");
                        preparationError = true;
                        errorMessage = $"Device preparation failed: {ex.Message}";
                    }
                }
            }
            #endregion
            
            // 準備段階でエラーが発生した場合はここで処理を中止
            if (preparationError)
            {
                isWriting = false;
                OnWriteError?.Invoke(errorMessage);
                StartRefreshRoutine();
                yield break;
            }

            // デバイスの準備に失敗した場合でも書き込みは試行する
            if (!devicePrepared)
            {
                Debug.Log("Proceeding with device write without special preparation");
            }

            #region 書き込み処理
            // 書き込みAPIの実行に関連する変数
            bool writeStarted = false;
            string writeErrorMessage = "";
            string writeStatus = null;
            
            // 書き込みAPIの呼び出しはtry-catchの外で行う
            yield return StartCoroutine(
                WriteISOWithCallback(
                    SelectedISO.name, 
                    SelectedDevice.id,
                    (status) => { 
                        writeStatus = status; 
                        writeStarted = true; 
                    },
                    (error) => { 
                        writeErrorMessage = error; 
                        writeStarted = false; 
                    }
                )
            );
            
            // 結果の処理はtry-catchで行う
            try
            {
                // 書き込み開始の成功/失敗をチェック
                if (writeStarted)
                {
                    // API呼び出しが成功した場合（実際の書き込みはバックグラウンドで進行）
                    Debug.Log($"Write operation started successfully: {writeStatus}");
                    // 書き込み開始は既に通知済み（StartWriting内で行う）
                }
                else
                {
                    // APIからエラーが返された場合
                    throw new Exception(writeErrorMessage);
                }
            }
            catch (Exception ex)
            {
                // 書き込み開始時のエラー処理
                Debug.LogError($"Failed to start writing: {ex.Message}");
                
                // 書き込みフラグを戻し、エラーイベントを発火
                isWriting = false;
                OnWriteError?.Invoke($"Failed to start writing: {ex.Message}");
                
                // 定期更新を再開
                StartRefreshRoutine();
            }
            #endregion
        }

        // コールバックを使ってデバイス準備を行うためのラッパーコルーチン
        private IEnumerator PrepareDeviceWithCallback(string deviceId, Action<bool> onSuccess, Action<string> onError)
        {
            yield return StartCoroutine(APIClient.Instance.PrepareDevice(deviceId, onSuccess, onError));
        }

        // コールバックを使って書き込みを行うためのラッパーコルーチン
        private IEnumerator WriteISOWithCallback(string isoName, string deviceId, Action<string> onSuccess, Action<string> onError)
        {
            yield return StartCoroutine(APIClient.Instance.WriteISOToDevice(isoName, deviceId, onSuccess, onError));
        }

        // 進捗更新のハンドラ
        private void HandleProgressUpdate(ProgressData progressData)
        {
            try
            {
                // 進捗データを通知する前にログを出力（デバッグ用）
                if (progressData.status != "idle" && progressData.status != lastLoggedStatus)
                {
                    Debug.Log($"Progress state: {progressData.status} ({progressData.progress}%)");
                    lastLoggedStatus = progressData.status;
                }
                
                // 完了検出時には常に詳細ログを出力
                if (progressData.status == "completed")
                {
                    Debug.Log($"[IMPORTANT] Completion detected: status={progressData.status}, progress={progressData.progress}%");
                }
                
                // イベント通知は必ず行う（注：これがないと進捗更新できない）
                OnWriteProgressUpdated?.Invoke(progressData);
                
                // 完了ステータスの処理 - 重複呼び出しを防止
                if (progressData.status == "completed" && isWriting)
                {
                    // 書き込み成功時に明示的なログ出力
                    Debug.Log("*** Write process completed successfully! ***");

                    // タイムアウト監視をキャンセル（重要: 状態変更前に行う）
                    CancelProgressTimeoutCheck();

                    // 書き込み状態をfalseに設定
                    isWriting = false;
                    
                    // 完了イベントを発火
                    Debug.Log("Invoking completion handlers");
                    OnWriteCompleted?.Invoke();
                    
                    // 書き込み完了後に定期更新を再開
                    StartRefreshRoutine();
                }
                // エラーステータスの処理
                else if (progressData.status != null && progressData.status.StartsWith("error"))
                {
                    // エラー処理も同様に、タイムアウトキャンセルを先に行う
                    CancelProgressTimeoutCheck();
                    
                    // エラー内容に応じたログ出力
                    if (progressData.status.Contains("1"))
                    {
                        Debug.LogWarning("Write failed with error code 1 - possibly write protected media");
                    }
                    else
                    {
                        Debug.LogWarning($"Write error: {progressData.status}");
                    }
                    
                    isWriting = false;
                    OnWriteError?.Invoke(progressData.status);
                    
                    // エラー発生後も定期更新を再開
                    StartRefreshRoutine();
                }
                // 95%以上で進捗が進まない場合のタイムアウト処理を開始
                else if (progressData.progress >= 95 && isWriting)
                {
                    StartProgressTimeoutCheck(progressData.progress);
                }
            }
            catch (Exception ex)
            {
                // ハンドラ内での例外を補足して、アプリケーションのクラッシュを防止
                Debug.LogError($"Exception in progress handler: {ex.Message}\n{ex.StackTrace}");
                
                // エラー状態を設定し、書き込みを終了
                isWriting = false;
                OnWriteError?.Invoke($"Internal error: {ex.Message}");
                
                // 例外発生時も定期更新を再開
                StartRefreshRoutine();
            }
        }
        
        // 最後にログに出力したステータス（同じステータスの繰り返しログを避けるため）
        private string lastLoggedStatus = "";
        
        private Coroutine progressTimeoutCoroutine;
        private int lastProgressValue;
        private float progressTimeoutDuration = 60f; // 1分間進捗が変わらなければタイムアウト
        
        // 進捗が止まっていないかチェックするコルーチンを開始
        private void StartProgressTimeoutCheck(int currentProgress)
        {
            // すでにコルーチンが実行中であれば何もしない
            if (progressTimeoutCoroutine != null)
            {
                // 進捗値が変わった場合は以前のタイマーをリセット
                if (lastProgressValue != currentProgress)
                {
                    CancelProgressTimeoutCheck();
                    lastProgressValue = currentProgress;
                    progressTimeoutCoroutine = StartCoroutine(CheckProgressTimeout());
                }
                return;
            }
            
            lastProgressValue = currentProgress;
            progressTimeoutCoroutine = StartCoroutine(CheckProgressTimeout());
        }
        
        // タイムアウトチェックをキャンセル
        private void CancelProgressTimeoutCheck()
        {
            if (progressTimeoutCoroutine != null)
            {
                StopCoroutine(progressTimeoutCoroutine);
                progressTimeoutCoroutine = null;
            }
        }
        
        // 進捗が一定時間更新されないかチェックするコルーチン
        private IEnumerator CheckProgressTimeout()
        {
            float elapsedTime = 0f;
            int initialProgress = lastProgressValue;
            bool highProgressDetected = lastProgressValue >= 99;
            float timeoutThreshold = highProgressDetected ? 20f : progressTimeoutDuration;
            
            while (elapsedTime < timeoutThreshold)
            {
                yield return new WaitForSeconds(5f);
                elapsedTime += 5f;
                
                // 進捗が変わった場合はタイマーをリセット
                if (lastProgressValue != initialProgress)
                {
                    progressTimeoutCoroutine = null;
                    yield break;
                }
                
                // 途中経過をログに出力
                if (elapsedTime % 15f == 0)
                {
                    Debug.Log($"Progress still at {lastProgressValue}% for {elapsedTime} seconds");
                }
                
                // 99%以上では短めのタイムアウト時間を適用
                if (lastProgressValue >= 99 && elapsedTime >= 15f)
                {
                    Debug.Log("High progress (99%+) has stalled. Completing write process.");
                    break; // タイムアウト処理に進む
                }
            }
            
            // タイムアウト - 書き込みを完了したとみなす
            Debug.LogWarning($"Progress timeout detected at {lastProgressValue}%. Forcing completion.");
            if (isWriting)
            {
                Debug.Log("Forcing completion due to timeout");
                isWriting = false;
                
                // 進捗100%の完了イベントを強制的に発生させる
                ProgressData completedData = new ProgressData 
                {
                    progress = 100,
                    status = "completed"
                };
                
                OnWriteProgressUpdated?.Invoke(completedData);
                OnWriteCompleted?.Invoke();
            }
            
            progressTimeoutCoroutine = null;
        }

        // サーバーの状態を確認するコルーチン
        private IEnumerator CheckServerStatus()
        {
            // APIClientにGetWriteStatusメソッドがないため、直接WebSocketClientを使用
            yield return new WaitForSeconds(0.5f);
            
            // WebSocketクライアントから直接状態を取得
            if (WebSocketClient.Instance != null)
            {
                // 強制的にポーリングを行い、最新状態を取得
                yield return StartCoroutine(WebSocketClient.Instance.ForcePollStatus(
                    status =>
                    {
                        Debug.Log($"Server write status check: progress={status.progress}, status={status.status}");
                        
                        // ステータスがcompletedだがクライアントが受信していない場合は手動で完了処理
                        if (status.status == "completed" && isWriting)
                        {
                            Debug.LogWarning("Server reports completed status but client didn't receive it. Manually completing.");
                            
                            // 完了イベントを強制発火
                            isWriting = false;
                            OnWriteProgressUpdated?.Invoke(status);
                            OnWriteCompleted?.Invoke();
                            
                            // タイムアウト監視をキャンセル
                            CancelProgressTimeoutCheck();
                        }
                    }
                ));
            }
            else
            {
                Debug.LogError("WebSocketClient instance not available");
            }
        }

        // 書き込み状態をリセットするメソッド
        public void ResetWritingState()
        {
            isWriting = false;
            
            // 最後にログに記録したステータスもリセットする
            lastLoggedStatus = "";
            
            // 進捗タイムアウトチェックをキャンセル
            CancelProgressTimeoutCheck();
        }

        // 選択されたデバイスをクリアするメソッド
        public void ClearSelectedDevice()
        {
            SelectedDevice = null;
        }

        // 状態を完全にリセットする新しいメソッド
        public void ResetAllState()
        {
            // 書き込み状態をリセット
            isWriting = false;
            lastLoggedStatus = "";
            // 選択状態もリセット
            SelectedISO = null;
            SelectedDevice = null;
            // 進捗タイムアウトチェックをキャンセル
            CancelProgressTimeoutCheck();
            
            // 定期更新を再開
            StartRefreshRoutine();
        }
    }
}