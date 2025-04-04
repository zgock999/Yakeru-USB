using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

namespace YakeruUSB
{
    /// <summary>
    /// ウィザードUIの画面制御を担当するコントローラ
    /// 関連ドキュメント: /docs/ui-architecture.md
    /// </summary>
    public class UIWizardScreenController : MonoBehaviour
    {
        [Header("Screens")]
        [SerializeField] private GameObject titleScreen;
        [SerializeField] private GameObject isoSelectionScreen;
        [SerializeField] private GameObject usbSelectionScreen; // USBSelectionScreenで統一
        [SerializeField] private GameObject confirmationScreen;
        [SerializeField] private GameObject writingScreen;
        [SerializeField] private GameObject resultScreen;

        [Header("Writing Screen")]
        [SerializeField] private Slider progressBar;
        [SerializeField] private TextMeshProUGUI progressText;
        [SerializeField] private TextMeshProUGUI statusText;

        [Header("Result Screen")]
        [SerializeField] private TextMeshProUGUI resultTitleText;
        [SerializeField] private TextMeshProUGUI resultMessageText;
        [SerializeField] private Image resultIcon;
        [SerializeField] private Sprite successIcon;
        [SerializeField] private Sprite errorIcon;
        [SerializeField] private Button finishButton;
        [SerializeField] private TextMeshProUGUI finishButtonText;

        [Header("Confirmation Screen")]
        [SerializeField] private TextMeshProUGUI confirmIsoNameText;
        [SerializeField] private TextMeshProUGUI confirmIsoSizeText;
        [SerializeField] private TextMeshProUGUI confirmDeviceNameText;
        [SerializeField] private TextMeshProUGUI confirmDeviceSizeText;
        [SerializeField] private TextMeshProUGUI warningText;
        [SerializeField] private Button writeButton;

        [Header("画面遷移設定")]
        [SerializeField] private bool useAnimations = true;
        [SerializeField] private float fadeInDuration = 0.3f;
        [SerializeField] private float fadeOutDuration = 0.2f;
        
        // UIWizard参照を追加
        [SerializeField] private UIWizard uiWizard;

        private enum ScreenType
        {
            Title,
            ISOSelection,
            DeviceSelection,
            Confirmation, // 確認画面の状態を追加
            Writing,
            Result
        }

        private ScreenType currentScreen;
        private ScreenType targetScreen;
        private TransitionState transitionState = TransitionState.Idle;

        private enum TransitionState
        {
            Idle,
            ExitingCurrent,
            EnteringNext
        }

        // 現在画面とターゲット画面のキャッシュ
        private GameObject currentScreenObj;
        private GameObject targetScreenObj;

        // 遷移中のコルーチン参照
        private Coroutine transitionCoroutine;

        // Start メソッドで初期化時にチェック
        private void Start()
        {
            // 起動時にWebSocketの状態をリセットする追加の対策
            if (WebSocketClient.Instance != null)
            {
                WebSocketClient.Instance.ResetConnectionState();
            }
            
            // UIWizardとの連携を確認
            if (uiWizard == null)
            {
                uiWizard = GetComponentInParent<UIWizard>() ?? FindAnyObjectByType<UIWizard>();
                if (uiWizard == null)
                {
                    Debug.LogWarning("UIWizard not found. Screen controller may not function properly.");
                }
            }

            // 初期画面をタイトル画面に設定
            currentScreen = ScreenType.Title;
            currentScreenObj = titleScreen;
            targetScreen = ScreenType.Title;
            targetScreenObj = titleScreen;
            transitionState = TransitionState.Idle;
            
            // すべての画面を非表示にしてからタイトル画面だけ表示
            HideAllScreens();
            titleScreen.SetActive(true);
        }

        private void Update()
        {
            // 遷移状態の更新を処理
            if (transitionState == TransitionState.ExitingCurrent)
            {
                // 現在画面の終了アニメーションが完了したか確認
                if (OnScreenExit(currentScreenObj))
                {
                    // 画面を非表示に
                    currentScreenObj.SetActive(false);
                    
                    // 次の画面の開始処理を開始
                    transitionState = TransitionState.EnteringNext;
                    targetScreenObj.SetActive(true);
                }
            }
            else if (transitionState == TransitionState.EnteringNext)
            {
                // 次画面の開始アニメーションが完了したか確認
                if (OnScreenEnter(targetScreenObj))
                {
                    // アニメーション完了、遷移を完了
                    transitionState = TransitionState.Idle;
                    currentScreen = targetScreen;
                    currentScreenObj = targetScreenObj;
                }
            }
        }

        private void HideAllScreens()
        {
            titleScreen.SetActive(false);
            isoSelectionScreen.SetActive(false);
            usbSelectionScreen.SetActive(false); // USBSelectionScreenのみ残す
            confirmationScreen.SetActive(false);
            writingScreen.SetActive(false);
            resultScreen.SetActive(false);
        }

        // 画面終了時の処理
        protected virtual bool OnScreenExit(GameObject screen)
        {
            if (!useAnimations || screen == null)
                return true;
            
            // ここにアニメーション処理を追加
            // スクリーンコントローラでは常にtrueを返す
            // 実際のアニメーション処理は派生クラスで実装
            
            return true;
        }

        // 画面開始時の処理
        protected virtual bool OnScreenEnter(GameObject screen)
        {
            if (!useAnimations || screen == null)
                return true;
            
            // ここにアニメーション処理を追加
            // スクリーンコントローラでは常にtrueを返す
            // 実際のアニメーション処理は派生クラスで実装
            
            return true;
        }

        // 新しい画面遷移メソッド
        private void TransitionToScreen(ScreenType newScreen, GameObject screenObject)
        {
            // 遷移中なら新しい遷移をスタートする前に現在の遷移を終了
            if (transitionState != TransitionState.Idle)
            {
                if (transitionCoroutine != null)
                {
                    StopCoroutine(transitionCoroutine);
                }
                if (currentScreenObj != null)
                {
                    // アニメーションをスキップして強制的に隠す
                    currentScreenObj.SetActive(false);
                }
            }
            
            targetScreen = newScreen;
            targetScreenObj = screenObject;
            
            // アニメーションを使用しない場合は直接切り替え
            if (!useAnimations)
            {
                HideAllScreens();
                screenObject.SetActive(true);
                currentScreen = newScreen;
                currentScreenObj = screenObject;
                transitionState = TransitionState.Idle;
                return;
            }
            
            // 初回遷移時
            if (currentScreenObj == null)
            {
                currentScreenObj = screenObject;
                HideAllScreens();
                screenObject.SetActive(true);
                currentScreen = newScreen;
                transitionState = TransitionState.Idle;
                OnScreenEnter(screenObject); // 初回表示はenterのみ
                return;
            }
            
            // 通常の画面遷移
            transitionState = TransitionState.ExitingCurrent;
        }

        // タイトル画面への遷移時にリセット処理を行わない
        public void TransitionToTitleScreen()
        {
            // タイトル画面への単純な遷移
            TransitionToScreen(ScreenType.Title, titleScreen);
            
            // 追加の初期化処理を行う場合はここに記述
            // 状態のリセットは行わない - これはUIWizard側で明示的に行う
        }

        // ISO選択画面への遷移
        public void TransitionToISOSelectionScreen()
        {
            TransitionToScreen(ScreenType.ISOSelection, isoSelectionScreen);
            
            // TODO: ISOリストの更新など
        }

        // デバイス選択画面への遷移（メソッド名は維持し、中身を変更）
        public void TransitionToDeviceSelectionScreen()
        {
            // usbSelectionScreenを直接使用
            TransitionToScreen(ScreenType.DeviceSelection, usbSelectionScreen);
            
            // TODO: デバイスリストの更新など
        }

        // 確認画面への遷移を追加
        public void TransitionToConfirmationScreen()
        {
            TransitionToScreen(ScreenType.Confirmation, confirmationScreen);
            
            // 確認画面の情報を更新
            UpdateConfirmationInfo();
        }

        // 確認画面の情報を更新するメソッド
        private void UpdateConfirmationInfo()
        {
            // 選択したISOとUSBデバイスの情報を表示
            if (ISOManager.Instance.SelectedISO != null && confirmIsoNameText != null && confirmIsoSizeText != null)
            {
                confirmIsoNameText.text = ISOManager.Instance.SelectedISO.name;
                confirmIsoSizeText.text = ISOManager.Instance.SelectedISO.size_formatted;
            }
            
            if (ISOManager.Instance.SelectedDevice != null && confirmDeviceNameText != null && confirmDeviceSizeText != null)
            {
                confirmDeviceNameText.text = ISOManager.Instance.SelectedDevice.name;
                confirmDeviceSizeText.text = ISOManager.Instance.SelectedDevice.size;
            }
            
            // 警告メッセージを表示
            if (warningText != null)
            {
                warningText.text = "警告: USBデバイス上のすべてのデータが消去されます。\n重要なデータはバックアップしてください。";
            }

            // 書き込みボタンのイベントを設定（必要に応じて）
            if (writeButton != null)
            {
                writeButton.onClick.RemoveAllListeners();
                writeButton.onClick.AddListener(() => {
                    TransitionToWritingScreen();
                });
            }
        }

        // 書き込み画面への遷移
        public void TransitionToWritingScreen()
        {
            TransitionToScreen(ScreenType.Writing, writingScreen);
            
            // 書き込み画面の初期化
            if (progressBar != null) progressBar.value = 0;
            if (progressText != null) progressText.text = "0%";
            if (statusText != null) statusText.text = "接続中...";  // 変更: より適切な初期メッセージ

            // StartWritingの呼び出しを別途行うように変更
            // 以前は書き込み処理を直接ここから開始していたが、
            // UIWizard側で書き込み開始→画面遷移の順序で呼び出すように変更
        }

        // 遷移完了後に書き込みを開始するコルーチン
        private IEnumerator StartWritingAfterTransition()
        {
            // 遷移状態が完了するまで待つ
            while (transitionState != TransitionState.Idle)
            {
                yield return null;
            }
            
            // 書き込み開始
            if (ISOManager.Instance != null)
            {
                ISOManager.Instance.StartWriting();
            }
        }

        // 進捗更新処理で競合しないよう、UpdateProgress メソッドをより堅牢に
        public void UpdateProgress(ProgressData progressData)
        {
            // アプリ起動直後は進捗更新を無視（アプリが完全に初期化されるまで）
            if (Time.realtimeSinceStartup < 3.0f)
            {
                Debug.Log($"Ignoring early progress update during startup: {progressData.status}");
                return;
            }
            
            // アプリが閉じる時の非表示処理を追加
            if (Time.frameCount <= 1 || !Application.isPlaying)
            {
                Debug.Log("Application is starting up or shutting down - ignoring progress update");
                return;
            }
            
            try
            {
                // 現在別の画面を表示している場合でも、重要なステータスは処理する
                if (progressData.status == "completed")
                {
                    Debug.Log("Completed status received - handling regardless of current screen");
                    
                    // 書き込み中かどうかをチェック - 必要な場合のみ完了処理を実行
                    if (ISOManager.Instance != null && ISOManager.Instance.IsWriting)
                    {
                        TransitionToResultScreen(true);
                    }
                    return;
                }
                else if (progressData.status != null && progressData.status.StartsWith("error"))
                {
                    Debug.Log($"Error status received: {progressData.status}");
                    
                    // 書き込み中かどうかをチェック - 必要な場合のみエラー処理を実行
                    if (ISOManager.Instance != null && ISOManager.Instance.IsWriting)
                    {
                        TransitionToResultScreen(false, progressData.status);
                    }
                    return;
                }
                
                // 書き込み画面以外では通常の進捗更新は無視するが
                // 開始通知（started, preparing_diskなど）は表示する必要がある
                bool isInitialStatus = progressData.status == "started" || 
                                     progressData.status == "preparing_disk" || 
                                     progressData.status == "cleaning_disk" ||
                                     progressData.status == "disk_prepared";
                                     
                if (currentScreen != ScreenType.Writing && !isInitialStatus)
                {
                    return;
                }

                // 進捗バーと進捗テキストの更新 - バックエンドの値をそのまま使用
                if (progressBar != null)
                    progressBar.value = progressData.progress / 100f;

                if (progressText != null)
                    progressText.text = $"{progressData.progress}%";

                if (statusText != null)
                {
                    string translatedStatus = WebSocketClient.GetStatusMessage(progressData.status);
                    
                    // 状態表示のみ更新、進捗の補正は不要
                    statusText.text = translatedStatus;
                    
                    // 95%以上で特別メッセージを表示するロジックは保持
                    if (progressData.progress >= 95 && progressData.status != "completed")
                    {
                        statusText.text = $"{translatedStatus} (もうしばらくお待ちください)";
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error in UpdateProgress: {ex.Message}");
            }
        }

        // 書き込み完了ハンドラを追加（UIWizardから呼び出される）
        public void HandleWriteCompleted()
        {
            // 結果画面に成功状態で遷移
            TransitionToResultScreen(true);
        }

        // 書き込みエラーハンドラを追加（UIWizardから呼び出される）
        public void HandleWriteError(string errorStatus)
        {
            // 結果画面に失敗状態で遷移
            TransitionToResultScreen(false, errorStatus);
        }

        // 結果画面への遷移メソッドを修正
        private void TransitionToResultScreen(bool success, string errorStatus = null)
        {
            TransitionToScreen(ScreenType.Result, resultScreen);
            
            // 結果画面の内容設定
            if (success)
            {
                if (resultTitleText != null) 
                    resultTitleText.text = "書き込み完了";
                if (resultMessageText != null)
                    resultMessageText.text = "USBメディアへのISOファイルの書き込みが完了しました。";
                if (resultIcon != null && successIcon != null)
                    resultIcon.sprite = successIcon;
                if (finishButtonText != null)
                    finishButtonText.text = "完了";
                
                // 完了ボタンのイベント変更 - バックエンドをリセットしてからタイトル画面に遷移
                if (finishButton != null)
                {
                    // ボタンが非表示になっていることがあるため、必ず表示に設定
                    finishButton.gameObject.SetActive(true);
                    
                    finishButton.onClick.RemoveAllListeners();
                    finishButton.onClick.AddListener(() => {
                        StartCoroutine(ResetBackendAndGoToTitle());
                    });
                }
            }
            else
            {
                if (resultTitleText != null)
                    resultTitleText.text = "書き込み失敗";

                if (resultMessageText != null)
                {
                    if (errorStatus != null && (errorStatus.Contains("1") || errorStatus.Contains("write_failed")))
                    {
                        resultMessageText.text = "USBメディアが書き込み禁止になっていないか確認してください。\n" + 
                                                "別のUSBメディアを試すか、別のポートに接続してみてください。";
                    }
                    else if (errorStatus != null && errorStatus.Contains("permission"))
                    {
                        resultMessageText.text = "権限が不足しています。\n管理者として実行してください。";
                    }
                    else
                    {
                        resultMessageText.text = errorStatus != null ? 
                            WebSocketClient.GetStatusMessage(errorStatus) : 
                            "書き込み中にエラーが発生しました。";
                    }
                }

                if (resultIcon != null && errorIcon != null)
                    resultIcon.sprite = errorIcon;

                if (finishButtonText != null)
                    finishButtonText.text = "閉じる";
                    
                // エラー時もfinishボタンが表示されるようにする
                if (finishButton != null)
                {
                    finishButton.gameObject.SetActive(true);
                }
            }
        }

        // バックエンドをリセットしてからタイトル画面に遷移するコルーチン
        private IEnumerator ResetBackendAndGoToTitle()
        {
            // バックエンドの状態をリセット
            yield return StartCoroutine(APIClient.Instance.ResetBackendStatus());
            
            // タイトル画面に遷移
            TransitionToTitleScreen();
        }

        // アニメーションの実装例: フェードイン
        protected virtual IEnumerator FadeInAnimation(GameObject screen)
        {
            CanvasGroup canvasGroup = screen.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
                canvasGroup = screen.AddComponent<CanvasGroup>();
                
            canvasGroup.alpha = 0;
            
            float startTime = Time.time;
            while (Time.time < startTime + fadeInDuration)
            {
                float t = (Time.time - startTime) / fadeInDuration;
                canvasGroup.alpha = Mathf.Lerp(0, 1, t);
                yield return null;
            }
            
            canvasGroup.alpha = 1;
        }

        // アニメーションの実装例: フェードアウト
        protected virtual IEnumerator FadeOutAnimation(GameObject screen)
        {
            CanvasGroup canvasGroup = screen.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
                canvasGroup = screen.AddComponent<CanvasGroup>();
                
            canvasGroup.alpha = 1;
            
            float startTime = Time.time;
            while (Time.time < startTime + fadeOutDuration)
            {
                float t = (Time.time - startTime) / fadeOutDuration;
                canvasGroup.alpha = Mathf.Lerp(1, 0, t);
                yield return null;
            }
            
            canvasGroup.alpha = 0;
        }

        // UIWizardからのイベント通知を受け取るメソッド
        public void OnISOSelected()
        {
            // ISOが選択されたときの処理（必要に応じて）
        }

        public void OnDeviceSelected()
        {
            // デバイスが選択されたときの処理（必要に応じて）
        }

        // UIWizardを経由せず直接ISOManagerの結果を処理する場合のハンドラ
        public void RegisterProgressHandlers()
        {
            if (ISOManager.Instance != null)
            {
                ISOManager.Instance.OnWriteProgressUpdated += UpdateProgress;
            }
        }

        public void UnregisterProgressHandlers()
        {
            if (ISOManager.Instance != null)
            {
                ISOManager.Instance.OnWriteProgressUpdated -= UpdateProgress;
            }
        }

        private void OnDestroy()
        {
            UnregisterProgressHandlers();
        }
    }
}