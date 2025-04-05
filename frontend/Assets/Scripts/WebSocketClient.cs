using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.Networking;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace YakeruUSB
{
    [Serializable]
    public class ProgressData
    {
        public int progress;
        public string status;
        // partition_info フィールドは削除またはコメントアウト
        public string phase;
    }

    public class WebSocketClient : MonoBehaviour
    {
        [SerializeField] private string socketUrl = "http://localhost:5000";
        [SerializeField] private float pollInterval = 2.0f; // ポーリング間隔を2秒に増やす
        [SerializeField] private bool enablePollingDebugLogs = false; // ポーリングログを制御するフラグ
        [SerializeField] private bool connectOnStart = true; // 起動時に自動接続するかどうかを制御するフラグ
        
        private bool isConnected = false;
        private bool isPolling = false;
        private ProgressData lastProgressData;

        // 進捗更新イベントを追加
        public event Action<ProgressData> OnProgressUpdated;

        // 初期進捗更新の無視フラグ
        private bool _ignoreInitialUpdates = true;

        // シングルトンインスタンス
        private static WebSocketClient _instance;
        public static WebSocketClient Instance
        {
            get
            {
                if (_instance == null)
                {
                    #if UNITY_EDITOR
                    _instance = FindAnyObjectByType<WebSocketClient>();
                    #else
                    _instance = FindAnyObjectByType<WebSocketClient>();
                    #endif
                    
                    if (_instance == null)
                    {
                        GameObject go = new GameObject("WebSocketClient");
                        _instance = go.AddComponent<WebSocketClient>();
                        
                        // DontDestroyOnLoadはプレイモードでのみ実行
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

        private void Start()
        {
            // 起動時に状態をリセット
            ResetConnectionState();
            
            // 以前の接続が残っている場合に備えて切断
            Disconnect();
            
            // バックエンドの状態も明示的にリセット
            StartCoroutine(ResetBackendStatus());
            
            // 必要に応じて自動接続
            if (connectOnStart)
            {
                Connect();
            }
        }

        private void OnApplicationQuit()
        {
            // アプリケーション終了時にシングルトンインスタンスをクリア
            _instance = null;
            
            // 接続を切断
            Disconnect();
            
            // MainThreadDispatcherのインスタンスが存在する場合は参照をクリア（新しいインスタンス生成を避ける）
            if (MainThreadDispatcher._instance != null)
            {
                MainThreadDispatcher._instance = null;
            }
        }

        private void OnDestroy()
        {
            Disconnect();
            
            // このインスタンスが現在のシングルトンインスタンスであれば、参照をクリア
            if (_instance == this)
            {
                _instance = null;
            }
            
            // OnDestroy内での新しいGameObjectの生成を避けるため、MainThreadDispatcherのクリーンアップは別の方法で行う
        }

        // 接続状態をリセットするメソッドを強化
        public void ResetConnectionState()
        {
            // 接続状態をリセット
            isConnected = false;
            isPolling = false;
            
            // 前回の進捗情報をクリア
            lastProgressData = null;
            
            // 既存のポーリングを停止
            StopAllCoroutines();
        }

        public void Connect()
        {
            if (isConnected)
            {
                return;
            }

            // 接続とポーリングを開始
            isConnected = true;
            StartPolling();
        }

        private void StartPolling()
        {
            if (!isPolling)
            {
                isPolling = true;
                StartCoroutine(PollProgressRoutine());
            }
        }

        private IEnumerator PollProgressRoutine()
        {
            while (isConnected && isPolling)
            {
                yield return StartCoroutine(PollProgress());
                yield return new WaitForSeconds(pollInterval);
            }
        }

        private IEnumerator PollProgress()
        {
            string url = $"{socketUrl}/api/write-status";
            using (UnityWebRequest request = UnityWebRequest.Get(url))
            {
                // タイムアウト設定を短めに調整して通信エラーをより早く検出
                request.timeout = 3;
                
                yield return request.SendWebRequest();

                if (request.result == UnityWebRequest.Result.Success)
                {
                    try
                    {
                        string json = request.downloadHandler.text;
                        
                        // 応答データをログに記録（診断用）- 常に完了状態を出力
                        if (json.Contains("completed"))
                        {
                            Debug.Log($"[CRITICAL] Detected completed status in response: {json}");
                        }
                        else if (enablePollingDebugLogs)
                        {
                            Debug.Log($"Poll response: {json}");
                        }
                        
                        JObject responseObj = JObject.Parse(json);

                        if (responseObj.ContainsKey("progress"))
                        {
                            int rawProgress = responseObj["progress"].Value<int>();
                            string status = responseObj["status"].Value<string>();
                            
                            // Linux環境での連続書き込み時の問題対策 - ISOManagerの状態とサーバー状態を比較
                            if (status == "completed" && 
                                ISOManager.Instance != null && 
                                !ISOManager.Instance.IsWriting)
                            {
                                // 既にISOManagerの書き込みフラグが下がっている場合は、
                                // 前回の書き込みの完了通知と判断して無視する
                                if (Application.platform == RuntimePlatform.LinuxEditor || 
                                    Application.platform == RuntimePlatform.LinuxPlayer)
                                {
                                    Debug.Log("Ignoring completed status from previous write operation");
                                    yield break;
                                }
                            }
                            
                            // 初期状態で完了ステータスを無視する場合の条件を変更
                            // 起動直後のみ無視し、その後はすべての完了通知を処理する
                            if (_ignoreInitialUpdates && status == "completed" && Time.realtimeSinceStartup < 5.0f)
                            {
                                Debug.Log("Ignoring initial completed status from server (startup period only)");
                                yield break;
                            }

                            // 進捗データを作成
                            ProgressData progress = new ProgressData
                            {
                                progress = rawProgress,
                                status = status
                            };

                            // 完了状態の場合は明示的に進捗を100%に設定
                            if (status == "completed")
                            {
                                Debug.Log("[IMPORTANT] Detected completed status - forcing progress to 100%");
                                progress.progress = 100;
                            }

                            // 高進捗時はポーリング間隔を短く設定
                            if (rawProgress >= 99 || status == "completed")
                            {
                                pollInterval = 0.2f; // 完了時または99%以上では非常に短い間隔で確認
                            }
                            else if (rawProgress >= 90)
                            {
                                pollInterval = 0.5f;
                            }
                            else
                            {
                                pollInterval = 1.0f;
                            }

                            // イベント発火 - null確認を厳密に行う
                            if (OnProgressUpdated != null)
                            {
                                try
                                {
                                    OnProgressUpdated.Invoke(progress);
                                }
                                catch (Exception ex)
                                {
                                    Debug.LogError($"Error invoking OnProgressUpdated: {ex.Message}");
                                }
                            }
                            else
                            {
                                Debug.LogWarning("OnProgressUpdated is null - no subscribers");
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        Debug.LogError($"Error parsing progress data: {e.Message}");
                        pollInterval = 2.0f;
                    }
                }
                else
                {
                    Debug.LogWarning($"Failed to poll progress: {request.error}");
                    // 通信エラーの場合は直接サーバー状態を確認
                    if (request.result == UnityWebRequest.Result.ConnectionError || 
                        request.result == UnityWebRequest.Result.ProtocolError)
                    {
                        Debug.Log("Connection issue detected, checking server directly");
                        yield return StartCoroutine(CheckDirectServerStatus());
                    }
                    pollInterval = 2.0f;
                }
            }
        }

        // サーバーの状態を直接確認する追加メソッド
        private IEnumerator CheckDirectServerStatus()
        {
            string url = $"{socketUrl}/api/write-status";
            using (UnityWebRequest request = UnityWebRequest.Get(url))
            {
                request.timeout = 5;
                yield return request.SendWebRequest();
                
                if (request.result == UnityWebRequest.Result.Success)
                {
                    try
                    {
                        string json = request.downloadHandler.text;
                        JObject responseObj = JObject.Parse(json);
                        
                        if (responseObj.ContainsKey("status") && responseObj["status"].Value<string>() == "completed")
                        {
                            Debug.Log("Direct check found completed status!");
                            
                            // 直接完了通知を生成
                            ProgressData progress = new ProgressData
                            {
                                progress = 100,
                                status = "completed"
                            };
                            
                            // イベント発火
                            if (OnProgressUpdated != null)
                            {
                                OnProgressUpdated.Invoke(progress);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"Error in direct status check: {ex.Message}");
                    }
                }
            }
        }

        // 状態を直接取得するメソッドを追加
        public IEnumerator ForcePollStatus(Action<ProgressData> callback)
        {
            string url = $"{socketUrl}/api/write-status";
            using (UnityWebRequest request = UnityWebRequest.Get(url))
            {
                yield return request.SendWebRequest();

                if (request.result == UnityWebRequest.Result.Success)
                {
                    try
                    {
                        string json = request.downloadHandler.text;
                        JObject responseObj = JObject.Parse(json);

                        if (responseObj.ContainsKey("progress"))
                        {
                            int rawProgress = responseObj["progress"].Value<int>();
                            string status = responseObj["status"].Value<string>();
                            
                            // 進捗データを作成
                            ProgressData progress = new ProgressData
                            {
                                progress = rawProgress,
                                status = status
                            };
                            
                            // コールバックで結果を返す
                            callback?.Invoke(progress);
                        }
                    }
                    catch (Exception e)
                    {
                        Debug.LogError($"Error parsing status data: {e.Message}");
                    }
                }
                else
                {
                    Debug.LogWarning($"Failed to check status: {request.error}");
                }
            }
        }

        private IEnumerator ResetBackendStatus()
        {
            string url = $"{socketUrl}/api/reset-status";
            using (UnityWebRequest request = UnityWebRequest.PostWwwForm(url, ""))
            {
                yield return request.SendWebRequest();
                
                if (request.result == UnityWebRequest.Result.Success)
                {
                    Debug.Log("Backend write status reset successfully");
                }
                else
                {
                    Debug.LogWarning($"Failed to reset backend status: {request.error}");
                }
            }
        }

        public void Disconnect()
        {
            isConnected = false;
            isPolling = false;
            StopAllCoroutines();
        }

        public void SimulateProgressUpdate(int progress, string status)
        {
            ProgressData progressData = new ProgressData
            {
                progress = progress,
                status = status
            };
            OnProgressUpdated?.Invoke(progressData);
        }

        public void ResetProgressState()
        {
            // 進捗状態をリセット
            ProgressData resetData = new ProgressData
            {
                progress = 0,
                status = "idle"
            };

            // 既存の状態を上書き
            lastProgressData = resetData;
            
            // 初期更新を無視するフラグを設定
            _ignoreInitialUpdates = true;
            
            // 3秒後にフラグをリセット
            StartCoroutine(ResetIgnoreFlag());
        }

        private IEnumerator ResetIgnoreFlag()
        {
            yield return new WaitForSeconds(3.0f);
            _ignoreInitialUpdates = false;
            Debug.Log("Now accepting progress updates");
        }

        public static string GetStatusMessage(string status)
        {
            switch (status)
            {
                case "started": return "準備中...";
                case "preparing": return "ディスクを準備中...";
                case "preparing_disk": return "ディスクを準備中...";
                case "cleaning_disk": return "ディスクをクリーンアップ中...";
                case "disk_prepared": return "ディスク準備完了";
                case "disabling_autoplay": return "自動再生を一時的に無効化中...";
                case "initial_write": return "初期データ書き込み中...";
                case var s when s.StartsWith("writing "): 
                    if (s.Contains("retry"))
                        return $"書き込みリトライ中... {s.Split('(')[1].Replace(')', ' ')} (リカバリー動作中)";
                    return s;
                case "writing": return "書き込み中...";
                case "main_write": return "メインデータ書き込み中...";
                case "checking_partitions": return "パーティション構成を確認中...";
                case "flushing": return "データをディスクに同期中...";
                case "syncing": return "ディスクを同期中...";
                case "finalizing": return "書き込みを完了中...";
                case "verifying": return "データを検証中...";
                case "completed": return "完了しました";
                case "opening_device": return "デバイスを開いています...";
                case "locking_volume": return "ボリュームをロック中...";
                case "dismounting_volume": return "ボリュームをアンマウント中...";
                case "error_permission_denied": return "エラー: 権限不足です。管理者として実行してください。";
                case "error_device_busy": return "エラー: デバイスが使用中です。デバイスがマウントされていないか確認してください。";
                case "error_write_failed": return "エラー: 書き込みに失敗しました。USBデバイスを確認してください。";
                case "error_code_1": return "エラー: 書き込み失敗 (エラーコード 1)。USBデバイスが書き込み禁止でないか確認してください。";
                default:
                    if (status.StartsWith("checking_volumes")) return "ボリュームを確認中...";
                    else if (status.StartsWith("dismounting_")) return "ドライブをアンマウント中...";
                    else if (status.StartsWith("error")) 
                    {
                        if (status.Contains("1")) return "エラー: 書き込み失敗。USBデバイスが書き込み禁止になっていないか確認してください。";
                        else if (status.Contains("permission")) return "エラー: 権限が不足しています。管理者として実行してください。";
                        else return "エラー: " + status.Replace("error_", "").Replace("_", " ");
                    }
                    else return status;
            }
        }

        private void OnWebSocketMessage(byte[] data)
        {
            string message = System.Text.Encoding.UTF8.GetString(data);
            
            try
            {
                ProgressData progressData = JsonUtility.FromJson<ProgressData>(message);
                
                // 前回と同じステータスのメッセージは無視（特に完了メッセージ）
                if (lastProgressData != null && 
                    lastProgressData.status == progressData.status && 
                    lastProgressData.progress == progressData.progress)
                {
                    return;
                }
                
                lastProgressData = progressData;
                
                // メインスレッドでイベントを発火
                MainThreadDispatcher.Execute(() => {
                    OnProgressUpdated?.Invoke(progressData);
                });
            }
            catch (Exception e)
            {
                Debug.LogError($"Error parsing WebSocket message: {e.Message}");
            }
        }
    }

    public class MainThreadDispatcher : MonoBehaviour
    {
        public static MainThreadDispatcher _instance;
        private readonly Queue<Action> _executionQueue = new Queue<Action>();
        private static int mainThreadId;

        public static MainThreadDispatcher Instance
        {
            get
            {
                if (ApplicationIsQuitting)
                {
                    return null;
                }

                if (_instance == null)
                {
                    #if UNITY_2022_3_OR_NEWER
                    _instance = FindAnyObjectByType<MainThreadDispatcher>();
                    #else
                    _instance = FindAnyObjectByType<MainThreadDispatcher>();
                    #endif

                    if (_instance == null && !ApplicationIsQuitting)
                    {
                        var go = new GameObject("MainThreadDispatcher");
                        _instance = go.AddComponent<MainThreadDispatcher>();
                        DontDestroyOnLoad(go);
                    }
                }
                return _instance;
            }
        }
        
        private static bool ApplicationIsQuitting = false;

        private void Awake()
        {
            if (_instance == null)
            {
                _instance = this;
                DontDestroyOnLoad(this.gameObject);
            }
            else if (_instance != this)
            {
                Destroy(gameObject);
            }
        }

        private void OnApplicationQuit()
        {
            ApplicationIsQuitting = true;
            _instance = null;
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }

        public void Update()
        {
            lock (_executionQueue)
            {
                while (_executionQueue.Count > 0)
                {
                    _executionQueue.Dequeue().Invoke();
                }
            }
        }

        public void Enqueue(Action action)
        {
            lock (_executionQueue)
            {
                _executionQueue.Enqueue(action);
            }
        }

        public static void Execute(Action action)
        {
            if (action == null) return;

            if (Thread.CurrentThread.IsMainThread())
            {
                action();
            }
            else
            {
                Instance.Enqueue(action);
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Init()
        {
            mainThreadId = Thread.CurrentThread.ManagedThreadId;
        }
    }

    public static class ThreadExtensions
    {
        private static int mainThreadId;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Init()
        {
            mainThreadId = Thread.CurrentThread.ManagedThreadId;
        }

        public static bool IsMainThread(this Thread thread)
        {
            return thread.ManagedThreadId == mainThreadId;
        }
    }
}
