using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

namespace YakeruUSB
{
    /// <summary>
    /// ウィザード形式でUSB書き込みを進めるUIコントローラ
    /// 関連ドキュメント: /docs/ui-architecture.md
    /// </summary>
    public class UIWizard : MonoBehaviour
    {
        [Header("ウィザードステップパネル")]
        [SerializeField] private GameObject titlePanel;
        [SerializeField] private GameObject isoSelectionPanel;
        [SerializeField] private GameObject usbSelectionPanel; // これを維持、DeviceSelectionPanelは不要
        [SerializeField] private GameObject confirmationPanel;
        [SerializeField] private GameObject writingPanel;
        [SerializeField] private GameObject completionPanel;

        [Header("ISOファイルリスト")]
        [SerializeField] private Transform isoListContent;
        [SerializeField] private GameObject isoListItemPrefab;
        [SerializeField] private TextMeshProUGUI selectedISOText;
        [SerializeField] private TextMeshProUGUI emptyISOListText; // 空のISOリスト表示用のテキスト
        [SerializeField] private string emptyISOListMessage = "ISOファイルがありません。\nbackend/isosフォルダにISOファイルを配置してください。";

        [Header("USBデバイスリスト")]
        [SerializeField] private Transform usbListContent;
        [SerializeField] private GameObject usbListItemPrefab;
        [SerializeField] private TextMeshProUGUI selectedUSBText;
        [SerializeField] private TextMeshProUGUI emptyUSBListText; // 空のUSBリスト表示用のテキスト
        [SerializeField] private string emptyUSBListMessage = "USBデバイスが見つかりません。\nUSBデバイスを接続して「更新」ボタンを押してください。";

        [Header("確認パネル")]
        [SerializeField] private TextMeshProUGUI confirmIsoNameText;
        [SerializeField] private TextMeshProUGUI confirmIsoSizeText;
        [SerializeField] private TextMeshProUGUI confirmDeviceNameText;
        [SerializeField] private TextMeshProUGUI confirmDeviceSizeText;
        [SerializeField] private TextMeshProUGUI warningText;

        [Header("進捗表示")]
        [SerializeField] private Slider progressBar;
        [SerializeField] private TextMeshProUGUI progressText;
        [SerializeField] private TextMeshProUGUI statusText;
        [SerializeField] private TextMeshProUGUI writingTitleText;

        [Header("完了パネル")]
        [SerializeField] private TextMeshProUGUI completionMessageText;
        [SerializeField] private TextMeshProUGUI resultMessageText;
        [SerializeField] private Image completionIcon;
        [SerializeField] private Sprite successIcon;
        [SerializeField] private Sprite errorIcon;
        [SerializeField] private TextMeshProUGUI resultTitleText;

        [Header("ナビゲーションボタン")]
        [SerializeField] private Button backButton;
        [SerializeField] private Button nextButton;
        [SerializeField] private Button startButton;
        [SerializeField] private Button writeButton;
        [SerializeField] private Button refreshIsoButton;
        [SerializeField] private Button refreshUsbButton;
        [SerializeField] private Button finishButton;
        [SerializeField] private Button quitButton;

        // 画面コントローラへの参照を追加
        [SerializeField] private UIWizardScreenController screenController;

        // ウィザードの現在のステップ
        private enum WizardStep
        {
            Title,
            IsoSelection,
            UsbSelection,
            Confirmation,
            Writing,
            Completion
        }

        private WizardStep currentStep = WizardStep.Title;
        private bool isWritingSuccess = false;
        private string errorMessage = "";

        private float targetProgress = 0f;
        private bool smoothingActive = false;

        private void Start()
        {
            // まず最初に、ISOManagerの状態を完全にリセット
            if (ISOManager.Instance != null)
            {
                ISOManager.Instance.ResetAllState();
            }
            
            // WebSocketClientの状態もリセット
            if (WebSocketClient.Instance != null)
            {
                WebSocketClient.Instance.ResetConnectionState();
                WebSocketClient.Instance.ResetProgressState();
            }
            
            // 画面コントローラの取得またはチェック
            if (screenController == null)
            {
                screenController = GetComponentInChildren<UIWizardScreenController>();
                if (screenController == null)
                {
                    Debug.LogError("UIWizardScreenController not found. Please attach it to a child GameObject.");
                }
            }

            // 初期ステップの設定
            ShowStep(WizardStep.Title);

            // ボタンのイベントハンドラを設定
            SetupButtonHandlers();
                        
            // マネージャーからのイベントを購読
            SubscribeToEvents();

            // 初期データの読み込み
            ISOManager.Instance.RefreshISOFiles();
            ISOManager.Instance.RefreshUSBDevices();
        }

        private void OnDestroy()
        {
            // イベントの購読解除
            UnsubscribeFromEvents();
        }

        #region イベント購読

        private void SubscribeToEvents()
        {
            ISOManager.Instance.OnISOFilesUpdated += UpdateISOList;
            ISOManager.Instance.OnUSBDevicesUpdated += UpdateUSBList;
            ISOManager.Instance.OnWriteProgressUpdated += UpdateProgress;
            ISOManager.Instance.OnWriteCompleted += OnWriteCompleted;
            ISOManager.Instance.OnWriteError += OnWriteError;
        }

        private void UnsubscribeFromEvents()
        {
            if (ISOManager.Instance != null)
            {
                ISOManager.Instance.OnISOFilesUpdated -= UpdateISOList;
                ISOManager.Instance.OnUSBDevicesUpdated -= UpdateUSBList;
                ISOManager.Instance.OnWriteProgressUpdated -= UpdateProgress;
                ISOManager.Instance.OnWriteCompleted -= OnWriteCompleted;
                ISOManager.Instance.OnWriteError -= OnWriteError;
            }
        }

        #endregion

        #region UI初期化とボタン設定

        private void SetupButtonHandlers()
        {
            // ナビゲーションボタン
            startButton.onClick.AddListener(() => ShowStep(WizardStep.IsoSelection));
            backButton.onClick.AddListener(OnBackButtonClicked);
            nextButton.onClick.AddListener(OnNextButtonClicked);
            writeButton.onClick.AddListener(OnWriteButtonClicked);
            
            // 完了ボタンの処理を変更
            finishButton.onClick.RemoveAllListeners(); // 既存のリスナーを削除
            finishButton.onClick.AddListener(() => {
                // 状態をリセットしてからタイトル画面に遷移
                StartCoroutine(ResetStateAndShowTitle());
            });
            
            // 更新ボタン
            refreshIsoButton.onClick.AddListener(() => ISOManager.Instance.RefreshISOFiles());
            refreshUsbButton.onClick.AddListener(() => ISOManager.Instance.RefreshUSBDevices());
               
            // 終了ボタン
            quitButton.onClick.AddListener(OnQuitButtonClicked);
        }

        // 状態のリセットとタイトル画面遷移を順番に実行するコルーチン
        private IEnumerator ResetStateAndShowTitle()
        {
            // まずバックエンドの状態をリセット
            yield return StartCoroutine(APIClient.Instance.ResetBackendStatus());
            
            // UIとマネージャーの状態をリセット
            ResetWizardState();
            
            // 定期更新を明示的に再開する
            if (ISOManager.Instance != null)
            {
                // 書き込み状態をリセットすることで、定期更新も再開される
                ISOManager.Instance.ResetAllState();
            }
            
            // タイトル画面に遷移
            ShowStep(WizardStep.Title);
        }

        // 書き込みプロセス完了後のウィザード状態をリセットするメソッド
        private void ResetWizardState()
        {
            // 選択状態をリセット
            if (ISOManager.Instance != null)
            {
                ISOManager.Instance.ClearSelectedDevice();
                ISOManager.Instance.ClearSelectedISO(); // ISOも明示的にリセット
                
                // 書き込み状態をリセット - ここで行う
                ISOManager.Instance.ResetWritingState();
            }
            
            // 進捗バーなどのUI状態をリセット
            if (progressBar != null) progressBar.value = 0;
            if (progressText != null) progressText.text = "0%";
            if (statusText != null) statusText.text = "";
            
            // 完了フラグもリセット
            isWritingSuccess = false;
            errorMessage = "";
        }

        private void OnBackButtonClicked()
        {
            switch (currentStep)
            {
                case WizardStep.IsoSelection:
                    ShowStep(WizardStep.Title);
                    break;
                case WizardStep.UsbSelection:
                    ShowStep(WizardStep.IsoSelection);
                    break;
                case WizardStep.Confirmation:
                    ShowStep(WizardStep.UsbSelection);
                    break;
                default:
                    // その他の場合は何もしない
                    break;
            }
        }

        private void OnNextButtonClicked()
        {
            switch (currentStep)
            {
                case WizardStep.IsoSelection:
                    if (ISOManager.Instance.SelectedISO != null)
                    {
                        ShowStep(WizardStep.UsbSelection);
                    }
                    break;
                case WizardStep.UsbSelection:
                    if (ISOManager.Instance.SelectedDevice != null)
                    {
                        ShowStep(WizardStep.Confirmation);
                    }
                    break;
                default:
                    // その他の場合は何もしない
                    break;
            }
        }

        private void OnWriteButtonClicked()
        {
            if (currentStep == WizardStep.Confirmation)
            {
                // 変更: 書き込み開始とUI遷移の順序を変更
                // まず画面遷移を行い、その後書き込み開始処理を実行
                ShowStep(WizardStep.Writing);
                
                // 書き込み開始処理をわずかに遅延させる（UIの安定のため）
                StartCoroutine(StartWritingWithDelay());
            }
        }

        // 短い遅延後に書き込みを開始するコルーチン
        private IEnumerator StartWritingWithDelay()
        {
            // UIが更新される時間を確保するため、1フレーム待機
            yield return null;
            
            // Linux環境での連続書き込み時の問題対策
            if (Application.platform == RuntimePlatform.LinuxEditor || 
                Application.platform == RuntimePlatform.LinuxPlayer)
            {
                // 念のため少し余分に待機
                yield return new WaitForSeconds(0.3f);
                
                // 強制的にバックエンドの状態をリセットしてから書き込み開始
                yield return StartCoroutine(APIClient.Instance.ResetBackendStatus());
                yield return new WaitForSeconds(0.5f);
            }
            
            // 書き込み開始
            ISOManager.Instance.StartWriting();
        }

        private void OnQuitButtonClicked()
        {
            // 終了確認
            if (ISOManager.Instance.IsWriting)
            {
                // 書き込み中なら確認ダイアログを表示
                ShowConfirmQuitDialog();
            }
            else
            {
                QuitApplication();
            }
        }

        private void ShowConfirmQuitDialog()
        {
            // 確認ダイアログを表示（実装は各自のUI構成に合わせてください）
            if (completionPanel != null)
            {
                completionMessageText.text = "書き込み中です。本当に終了しますか？\n進行中の書き込みが中断されます。";
                completionIcon.sprite = errorIcon;
                
                finishButton.gameObject.SetActive(false);
                
                // 確認用のカスタムボタンを表示
                Transform confirmButtonTransform = completionPanel.transform.Find("ConfirmQuitButton");
                if (confirmButtonTransform != null)
                {
                    Button confirmButton = confirmButtonTransform.GetComponent<Button>();
                    confirmButton.gameObject.SetActive(true);
                    confirmButton.onClick.RemoveAllListeners();
                    confirmButton.onClick.AddListener(QuitApplication);
                }
                
                Transform cancelButtonTransform = completionPanel.transform.Find("CancelButton");
                if (cancelButtonTransform != null)
                {
                    Button cancelButton = cancelButtonTransform.GetComponent<Button>();
                    cancelButton.gameObject.SetActive(true);
                    cancelButton.onClick.RemoveAllListeners();
                    cancelButton.onClick.AddListener(() => ShowStep(currentStep));
                }
                   
                HideAllStepPanels();
                completionPanel.SetActive(true);
            }
        }

        private void QuitApplication()
        {
            // WebSocketの切断
            if (WebSocketClient.Instance != null)
            {
                WebSocketClient.Instance.Disconnect();
            }
                
            StartCoroutine(DelayedQuit());
        }
        
        private System.Collections.IEnumerator DelayedQuit()
        {
            yield return new WaitForSeconds(0.5f);
            
            #if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
            #else
            Application.Quit();
            #endif
        }

        #endregion

        #region ステップ制御

        private void ShowStep(WizardStep step)
        {
            currentStep = step;
            
            // 全てのパネルを非表示
            HideAllStepPanels();
            
            // ナビゲーションボタンの状態設定
            UpdateNavigationButtons();
            
            // 現在のステップに応じたパネルを表示と、画面コントローラも連動
            switch (step)
            {
                case WizardStep.Title:
                    titlePanel.SetActive(true);
                    break;
                    
                case WizardStep.IsoSelection:
                    isoSelectionPanel.SetActive(true);
                    UpdateISOSelectionUI();
                    break;
                    
                case WizardStep.UsbSelection:
                    usbSelectionPanel.SetActive(true);
                    UpdateUSBSelectionUI();
                    break;
                    
                case WizardStep.Confirmation:
                    confirmationPanel.SetActive(true);
                    UpdateConfirmationUI();
                    break;
                    
                case WizardStep.Writing:
                    writingPanel.SetActive(true);
                    InitializeWritingUI();
                    // 画面コントローラを書き込み画面に遷移
                    if (screenController != null)
                    {
                        screenController.TransitionToWritingScreen();
                    }
                    break;
                    
                case WizardStep.Completion:
                    completionPanel.SetActive(true);
                    UpdateCompletionUI();
                    break;
            }
        }

        private void HideAllStepPanels()
        {
            titlePanel.SetActive(false);
            isoSelectionPanel.SetActive(false);
            usbSelectionPanel.SetActive(false);
            confirmationPanel.SetActive(false);
            writingPanel.SetActive(false);
            completionPanel.SetActive(false);
        }

        private void UpdateNavigationButtons()
        {
            // 戻るボタンはタイトル画面と書き込み中、完了画面では非表示
            backButton.gameObject.SetActive(
                currentStep != WizardStep.Title && 
                currentStep != WizardStep.Writing && 
                currentStep != WizardStep.Completion
            );
            
            // 次へボタンはISO選択とUSB選択でのみ表示
            nextButton.gameObject.SetActive(
                currentStep == WizardStep.IsoSelection || 
                currentStep == WizardStep.UsbSelection
            );
            
            // 次へボタンの有効/無効状態
            if (currentStep == WizardStep.IsoSelection)
            {
                nextButton.interactable = ISOManager.Instance.SelectedISO != null;
            }
            else if (currentStep == WizardStep.UsbSelection)
            {
                nextButton.interactable = ISOManager.Instance.SelectedDevice != null;
            }
            
            // 書き込みボタンは確認画面でのみ表示
            writeButton.gameObject.SetActive(currentStep == WizardStep.Confirmation);
               
            // 終了/タイトルに戻るボタンは完了画面でのみ表示
            finishButton.gameObject.SetActive(currentStep == WizardStep.Completion);
        }

        #endregion

        #region UI更新

        private void UpdateISOList(List<ISOFile> isoFiles)
        {
            // 既存のリストアイテムをクリア
            foreach (Transform child in isoListContent)
            {
                Destroy(child.gameObject);
            }
            
            // ISOファイルがない場合のメッセージを表示
            if (isoFiles.Count == 0)
            {
                if (emptyISOListText != null)
                {
                    // 既存のテキストオブジェクトを表示し、メッセージを設定
                    emptyISOListText.gameObject.SetActive(true);
                    emptyISOListText.text = emptyISOListMessage;
                }
            }
            else
            {
                // ISOファイルがある場合は空メッセージを非表示
                if (emptyISOListText != null)
                {
                    emptyISOListText.gameObject.SetActive(false);
                }
                
                // 各ISOファイルのリストアイテムを作成
                foreach (var iso in isoFiles)
                {
                    GameObject item = Instantiate(isoListItemPrefab, isoListContent);
                    
                    // レイアウト設定
                    ConfigureListItemLayout(item);
                    
                    // 項目のテキストを設定
                    TextMeshProUGUI isoNameText = item.transform.Find("NameText").GetComponent<TextMeshProUGUI>();
                    TextMeshProUGUI isoSizeText = item.transform.Find("SizeText").GetComponent<TextMeshProUGUI>();
                    
                    isoNameText.text = iso.name;
                    isoSizeText.text = iso.size_formatted;
                    
                    // 選択状態の初期設定
                    bool isSelected = ISOManager.Instance.SelectedISO != null && 
                                     ISOManager.Instance.SelectedISO.name == iso.name;
                    item.GetComponent<Image>().color = isSelected ? 
                        new Color(0.2f, 0.5f, 0.9f) : new Color(0.2f, 0.2f, 0.2f);
                    
                    // クリックイベントを設定
                    int index = isoFiles.IndexOf(iso); // ラムダでのキャプチャ用
                    Button button = item.GetComponent<Button>();
                    button.onClick.AddListener(() => OnISOItemClicked(iso, index));
                }
            }
            
            // ISP選択画面が表示中なら選択状態を更新
            if (currentStep == WizardStep.IsoSelection)
            {
                UpdateISOSelectionUI();
            }
        }

        private void UpdateUSBList(List<USBDevice> usbDevices)
        {
            // 既存のリストアイテムをクリア
            foreach (Transform child in usbListContent)
            {
                Destroy(child.gameObject);
            }
            
            // USBデバイスがない場合のメッセージを表示
            if (usbDevices.Count == 0)
            {
                if (emptyUSBListText != null)
                {
                    // 既存のテキストオブジェクトを表示し、メッセージを設定
                    emptyUSBListText.gameObject.SetActive(true);
                    emptyUSBListText.text = emptyUSBListMessage;
                }
            }
            else
            {
                // USBデバイスがある場合は空メッセージを非表示
                if (emptyUSBListText != null)
                {
                    emptyUSBListText.gameObject.SetActive(false);
                }
                
                // 各USBデバイスのリストアイテムを作成
                foreach (var device in usbDevices)
                {
                    GameObject item = Instantiate(usbListItemPrefab, usbListContent);
                    
                    // レイアウト設定
                    ConfigureListItemLayout(item);
                    
                    // 項目のテキストを設定
                    TextMeshProUGUI deviceNameText = item.transform.Find("NameText").GetComponent<TextMeshProUGUI>();
                    TextMeshProUGUI deviceSizeText = item.transform.Find("SizeText").GetComponent<TextMeshProUGUI>();
                    
                    deviceNameText.text = device.name;
                    deviceSizeText.text = device.size;
                    
                    // 選択状態の初期設定
                    bool isSelected = ISOManager.Instance.SelectedDevice != null && 
                                     ISOManager.Instance.SelectedDevice.id == device.id;
                    item.GetComponent<Image>().color = isSelected ? 
                        new Color(0.2f, 0.5f, 0.9f) : new Color(0.2f, 0.2f, 0.2f);
                    
                    // クリックイベントを設定
                    int index = usbDevices.IndexOf(device); // ラムダでのキャプチャ用
                    Button button = item.GetComponent<Button>();
                    button.onClick.AddListener(() => OnUSBItemClicked(device, index));
                }
            }
            
            // USB選択画面が表示中なら選択状態を更新
            if (currentStep == WizardStep.UsbSelection)
            {
                UpdateUSBSelectionUI();
            }
        }

        private void ConfigureListItemLayout(GameObject item)
        {
            // リストアイテムのレイアウト設定
            LayoutElement layoutElement = item.GetComponent<LayoutElement>();
            if (layoutElement == null)
            {
                layoutElement = item.AddComponent<LayoutElement>();
            }
            
            // 高さと幅の設定
            layoutElement.preferredHeight = 60;
            layoutElement.flexibleWidth = 1;
            
            // アンカー設定
            RectTransform rectTransform = item.GetComponent<RectTransform>();
            rectTransform.anchorMin = new Vector2(0, 0);
            rectTransform.anchorMax = new Vector2(1, 0);
            rectTransform.pivot = new Vector2(0.5f, 0);
        }

        private void OnISOItemClicked(ISOFile iso, int index)
        {
            // ISOファイルを選択
            ISOManager.Instance.SelectISO(iso);
            
            // UIの更新
            UpdateISOSelectionUI();
            
            // リストアイテムの選択状態を視覚的に更新
            for (int i = 0; i < isoListContent.childCount; i++)
            {
                Transform child = isoListContent.GetChild(i);
                if (child.GetComponent<Button>() != null)
                {
                    child.GetComponent<Image>().color = (i == index) ? 
                        new Color(0.2f, 0.5f, 0.9f) : new Color(0.2f, 0.2f, 0.2f);
                }
            }
            
            // 次へボタンを有効化
            nextButton.interactable = true;
        }

        private void OnUSBItemClicked(USBDevice device, int index)
        {
            // USBデバイスを選択
            ISOManager.Instance.SelectDevice(device);
            
            // UIの更新
            UpdateUSBSelectionUI();
            
            // リストアイテムの選択状態を視覚的に更新
            for (int i = 0; i < usbListContent.childCount; i++)
            {
                Transform child = usbListContent.GetChild(i);
                if (child.GetComponent<Button>() != null)
                {
                    child.GetComponent<Image>().color = (i == index) ? 
                        new Color(0.2f, 0.5f, 0.9f) : new Color(0.2f, 0.2f, 0.2f);
                }
            }
            
            // 次へボタンを有効化
            nextButton.interactable = true;
        }

        private void UpdateISOSelectionUI()
        {
            // 選択中のISOファイル情報を表示
            if (ISOManager.Instance.SelectedISO != null)
            {
                selectedISOText.text = $"選択中: {ISOManager.Instance.SelectedISO.name} ({ISOManager.Instance.SelectedISO.size_formatted})";
            }
            else
            {
                selectedISOText.text = "ISOファイルを選択してください";
            }
            
            // ナビゲーションボタンの状態を更新
            UpdateNavigationButtons();
        }

        private void UpdateUSBSelectionUI()
        {
            // 選択中のUSBデバイス情報を表示
            if (ISOManager.Instance.SelectedDevice != null)
            {
                selectedUSBText.text = $"選択中: {ISOManager.Instance.SelectedDevice.name} ({ISOManager.Instance.SelectedDevice.size})";
            }
            else
            {
                selectedUSBText.text = "USBデバイスを選択してください";
            }
            
            // ナビゲーションボタンの状態を更新
            UpdateNavigationButtons();
        }

        private void UpdateConfirmationUI()
        {
            // 選択したISOとUSBデバイスの情報を表示
            if (ISOManager.Instance.SelectedISO != null)
            {
                confirmIsoNameText.text = ISOManager.Instance.SelectedISO.name;
                confirmIsoSizeText.text = ISOManager.Instance.SelectedISO.size_formatted;
            }
            
            if (ISOManager.Instance.SelectedDevice != null)
            {
                confirmDeviceNameText.text = ISOManager.Instance.SelectedDevice.name;
                confirmDeviceSizeText.text = ISOManager.Instance.SelectedDevice.size;
            }
            
            // 警告メッセージ
            warningText.text = "警告: USBデバイス上のすべてのデータが消去されます。\n重要なデータはバックアップしてください。";
        }

        private void InitializeWritingUI()
        {
            // 進捗バーを初期化
            progressBar.value = 0;
            progressText.text = "0%";
            statusText.text = "準備中...";
        }

        private void UpdateCompletionUI()
        {
            // 成功/失敗に応じたメッセージとアイコンを表示
            if (isWritingSuccess)
            {
                completionMessageText.text = "書き込みが完了しました！\nUSBデバイスを安全に取り外してください。";
                completionIcon.sprite = successIcon;
            }
            else
            {
                completionMessageText.text = $"書き込みに失敗しました。\n{errorMessage}";
                completionIcon.sprite = errorIcon;
            }
            
            // 完了時のUI用のボタンの設定 - 必ず表示する
            finishButton.gameObject.SetActive(true);
            
            // 結果タイトルの設定（追加）
            if (resultTitleText != null)
            {
                resultTitleText.text = isWritingSuccess ? "書き込み完了" : "書き込み失敗";
            }
            
            // 確認ボタンやキャンセルボタンが表示されていたら非表示にする
            Transform confirmButtonTransform = completionPanel.transform.Find("ConfirmQuitButton");
            if (confirmButtonTransform != null)
            {
                confirmButtonTransform.gameObject.SetActive(false);
            }
            
            Transform cancelButtonTransform = completionPanel.transform.Find("CancelButton");
            if (cancelButtonTransform != null)
            {
                cancelButtonTransform.gameObject.SetActive(false);
            }
        }

        #endregion

        #region 書き込み関連イベント処理

        // 画面コントローラ経由で進捗を更新
        private void UpdateProgress(ProgressData progressData)
        {
            // 画面コントローラが存在する場合は、そちらに進捗更新を委譲する
            if (screenController != null)
            {
                screenController.UpdateProgress(progressData);
                return; // ここで早期リターンして重複処理を避ける
            }
            
            // screenController がない場合のフォールバック処理（以下は既存のコード）
            // 書き込み中でなければ進捗更新を無視
            if (!ISOManager.Instance.IsWriting && progressData.status != "starting")
            {
                return;
            }
            
            // 進捗情報の更新
            writingPanel.SetActive(true);
            
            // 状態に応じたUI表示を調整
            UpdateStatusDisplay(progressData);
            
            // 目標進捗値を設定（アニメーションのため）
            targetProgress = progressData.progress / 100f;
            
            // スムージングが有効でない場合は開始
            if (!smoothingActive)
            {
                smoothingActive = true;
                StartCoroutine(SmoothProgressAnimation());
            }
            
            // 進捗テキストはすぐに更新
            progressText.text = $"{progressData.progress}%";
            
            // 完了時は完了画面に移行
            if (progressData.status == "completed")
            {
                smoothingActive = false;
                ShowCompletionScreen();
            }
            // エラー時はエラー画面に移行
            else if (progressData.status.StartsWith("error"))
            {
                smoothingActive = false;
                ShowErrorScreen(progressData.status);
            }
        }

        // 状態に応じたステータス表示の更新
        private void UpdateStatusDisplay(ProgressData progressData)
        {
            string translatedStatus = WebSocketClient.GetStatusMessage(progressData.status);
            
            // 95%以上で特別メッセージを表示するロジックは保持
            if (progressData.progress >= 95 && progressData.status != "completed")
            {
                statusText.text = $"{translatedStatus} (しばらくお待ちください)";
            }
            else
            {
                statusText.text = translatedStatus;
            }
        }
        
        // 進捗バーをスムーズにアニメーションさせるためのコルーチン
        private System.Collections.IEnumerator SmoothProgressAnimation()
        {
            // 現在値から目標値へ徐々に近づける
            while (smoothingActive)
            {
                // 現在の進捗値と目標値の差
                float difference = targetProgress - progressBar.value;
                
                // 差が非常に小さい場合はスキップ
                if (Mathf.Abs(difference) < 0.001f)
                {
                    yield return new WaitForSeconds(0.1f);
                    continue;
                }
                
                // 進捗が増加する場合はより素早く、減少する場合はゆっくりと
                float step = difference > 0 ? 
                    Mathf.Min(difference, Time.deltaTime * 0.5f) : 
                    Mathf.Max(difference, -Time.deltaTime * 0.2f);
                    
                progressBar.value += step;
                
                yield return null;
            }
            
            // スムージング終了、最終値に設定
            progressBar.value = targetProgress;
        }
        
        // 書き込み完了画面へ移動
        private void ShowCompletionScreen()
        {
            // 書き込み成功時の処理
            isWritingSuccess = true;
            ShowStep(WizardStep.Completion);
        }

        private void OnWriteStarted()
        {
            Debug.Log("Write process started - periodic updates paused");
            
            // 選択画面を非表示にする
            if (usbSelectionPanel != null) {
                usbSelectionPanel.SetActive(false);
            }
            
            // 書き込み画面を表示
            writingPanel.SetActive(true);
            
            // 進捗をリセット
            progressBar.value = 0;
            progressText.text = "0%";
            statusText.text = "準備中..."; // 初期メッセージをより明確に
            
            // 進捗値をリセット
            targetProgress = 0f;
            smoothingActive = false;
            
            // タイトル更新
            if (writingTitleText != null) {
                writingTitleText.text = "書き込み中...";
            }
        }

        private void ShowErrorScreen(string errorStatus)
        {
            // 書き込み画面を非表示
            writingPanel.SetActive(false);
            
            // 結果画面を表示 - resultPanel の参照を completionPanel に変更
            completionPanel.SetActive(true);
            
            // エラー内容に応じたメッセージを表示
            if (resultTitleText != null && resultMessageText != null) {
                if (errorStatus.Contains("1") || errorStatus.Contains("write_failed"))
                {
                    resultTitleText.text = "書き込みに失敗しました";
                    resultMessageText.text = "USBメディアが書き込み禁止になっていないか確認してください。\n" +
                                            "また、USBメディアが正常に接続されているか確認してください。\n" +
                                            "別のUSBポートや別のUSBメディアを試してみることも有効です。";
                }
                else if (errorStatus.Contains("permission"))
                {
                    resultTitleText.text = "権限エラー";
                    resultMessageText.text = "書き込みに必要な権限がありません。\n" +
                                            "このアプリを管理者権限で実行してください。";
                }
                else
                {
                    resultTitleText.text = "エラーが発生しました";
                    resultMessageText.text = WebSocketClient.GetStatusMessage(errorStatus);
                }
            }
            
            // ボタンのテキストを設定
            if (finishButton != null && finishButton.GetComponentInChildren<TextMeshProUGUI>() != null) {
                finishButton.GetComponentInChildren<TextMeshProUGUI>().text = "終了";
            }
            
            // アイコンをエラー表示に
            if (completionIcon != null && errorIcon != null)
            {
                completionIcon.sprite = errorIcon;
            }
        }

        private void OnWriteCompleted()
        {
            // 画面コントローラが存在する場合は、完了処理を委譲
            if (screenController != null)
            {
                // UIWizardScreenControllerの完了処理を呼び出す
                screenController.HandleWriteCompleted();
                return; // 重複処理を避けるため早期リターン
            }
            
            // 以下は従来の処理（screenControllerがない場合のフォールバック）
            isWritingSuccess = true;
            ShowStep(WizardStep.Completion);
            
            // USBデバイスリストを更新（念のため）
            ISOManager.Instance.RefreshUSBDevices();
        }

        private void OnWriteError(string error)
        {
            // 画面コントローラが存在する場合は、エラー処理を委譲
            if (screenController != null)
            {
                // UIWizardScreenControllerのエラー処理を呼び出す
                screenController.HandleWriteError(error);
                return; // 重複処理を避けるため早期リターン
            }
            
            // 以下は従来の処理（screenControllerがない場合のフォールバック）
            isWritingSuccess = false;
            errorMessage = error;
            ShowStep(WizardStep.Completion);
        }

        #endregion
    }
}