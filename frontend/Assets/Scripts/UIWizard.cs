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
    /// </summary>
    public class UIWizard : MonoBehaviour
    {
        [Header("ウィザードステップパネル")]
        [SerializeField] private GameObject titlePanel;
        [SerializeField] private GameObject isoSelectionPanel;
        [SerializeField] private GameObject usbSelectionPanel;
        [SerializeField] private GameObject confirmationPanel;
        [SerializeField] private GameObject writingPanel;
        [SerializeField] private GameObject completionPanel;

        [Header("ISOファイルリスト")]
        [SerializeField] private Transform isoListContent;
        [SerializeField] private GameObject isoListItemPrefab;
        [SerializeField] private TextMeshProUGUI selectedISOText;

        [Header("USBデバイスリスト")]
        [SerializeField] private Transform usbListContent;
        [SerializeField] private GameObject usbListItemPrefab;
        [SerializeField] private TextMeshProUGUI selectedUSBText;

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

        [Header("完了パネル")]
        [SerializeField] private TextMeshProUGUI completionMessageText;
        [SerializeField] private Image completionIcon;
        [SerializeField] private Sprite successIcon;
        [SerializeField] private Sprite errorIcon;

        [Header("ナビゲーションボタン")]
        [SerializeField] private Button backButton;
        [SerializeField] private Button nextButton;
        [SerializeField] private Button startButton; // タイトル画面のスタートボタン
        [SerializeField] private Button writeButton; // 確認画面の書き込みボタン
        [SerializeField] private Button refreshIsoButton;
        [SerializeField] private Button refreshUsbButton;
        [SerializeField] private Button finishButton; // 完了画面のタイトルに戻るボタン
        [SerializeField] private Button quitButton; // 終了ボタン

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
            finishButton.onClick.AddListener(() => ShowStep(WizardStep.Title));
            
            // 更新ボタン
            refreshIsoButton.onClick.AddListener(() => ISOManager.Instance.RefreshISOFiles());
            refreshUsbButton.onClick.AddListener(() => ISOManager.Instance.RefreshUSBDevices());
            
            // 終了ボタン
            quitButton.onClick.AddListener(OnQuitButtonClicked);
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
                ISOManager.Instance.StartWriting();
                ShowStep(WizardStep.Writing);
            }
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
        
        private IEnumerator DelayedQuit()
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
            
            // 現在のステップに応じたパネルを表示
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
            
            // ISOファイルがない場合のメッセージ
            if (isoFiles.Count == 0)
            {
                GameObject emptyItem = new GameObject("EmptyMessage");
                emptyItem.transform.SetParent(isoListContent, false);
                
                TextMeshProUGUI text = emptyItem.AddComponent<TextMeshProUGUI>();
                text.text = "ISOファイルがありません。\nbackend/isosフォルダにISOファイルを配置してください。";
                text.fontSize = 14;
                text.alignment = TextAlignmentOptions.Center;
                text.color = Color.white;
                
                RectTransform rect = emptyItem.GetComponent<RectTransform>();
                rect.sizeDelta = new Vector2(300, 60);
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
            
            // USBデバイスがない場合のメッセージ
            if (usbDevices.Count == 0)
            {
                GameObject emptyItem = new GameObject("EmptyMessage");
                emptyItem.transform.SetParent(usbListContent, false);
                
                TextMeshProUGUI text = emptyItem.AddComponent<TextMeshProUGUI>();
                text.text = "USBデバイスが見つかりません。\nUSBデバイスを接続して「更新」ボタンを押してください。";
                text.fontSize = 14;
                text.alignment = TextAlignmentOptions.Center;
                text.color = Color.white;
                
                RectTransform rect = emptyItem.GetComponent<RectTransform>();
                rect.sizeDelta = new Vector2(300, 60);
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
            
            // 完了時のUI用のボタンの設定
            finishButton.gameObject.SetActive(true);
            
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

        private void UpdateProgress(ProgressData progressData)
        {
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
            statusText.text = WebSocketClient.GetStatusMessage(progressData.status);
            
            // 完了したらウィザードを次へ進める
            if (progressData.status == "completed")
            {
                smoothingActive = false;
                isWritingSuccess = true;
                ShowStep(WizardStep.Completion);
            }
        }
        
        // 進捗バーをスムーズにアニメーションさせるためのコルーチン
        private IEnumerator SmoothProgressAnimation()
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

        private void OnWriteCompleted()
        {
            isWritingSuccess = true;
            ShowStep(WizardStep.Completion);
            
            // USBデバイスリストを更新（念のため）
            ISOManager.Instance.RefreshUSBDevices();
        }

        private void OnWriteError(string error)
        {
            isWritingSuccess = false;
            errorMessage = error;
            ShowStep(WizardStep.Completion);
        }

        #endregion
    }
}
