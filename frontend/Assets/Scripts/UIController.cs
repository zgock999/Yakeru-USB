using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace YakeruUSB
{
    public class UIController : MonoBehaviour
    {
        [Header("ISO File List")]
        [SerializeField] private Transform isoListContent;
        [SerializeField] private GameObject isoListItemPrefab;
        
        [Header("USB Device List")]
        [SerializeField] private Transform usbListContent;
        [SerializeField] private GameObject usbListItemPrefab;
        
        [Header("Controls")]
        [SerializeField] private Button refreshISOsButton;
        [SerializeField] private Button refreshDevicesButton;
        [SerializeField] private Button writeButton;
        
        [Header("Info Panels")]
        [SerializeField] private GameObject selectedISOPanel;
        [SerializeField] private TextMeshProUGUI selectedISONameText;
        [SerializeField] private TextMeshProUGUI selectedISOSizeText;
        
        [SerializeField] private GameObject selectedDevicePanel;
        [SerializeField] private TextMeshProUGUI selectedDeviceNameText;
        [SerializeField] private TextMeshProUGUI selectedDeviceSizeText;
        
        [Header("Progress")]
        [SerializeField] private GameObject progressPanel;
        [SerializeField] private Slider progressSlider;
        [SerializeField] private TextMeshProUGUI progressText;
        [SerializeField] private TextMeshProUGUI statusText;
        
        [Header("Messages")]
        [SerializeField] private GameObject messagePanel;
        [SerializeField] private TextMeshProUGUI messageText;
        [SerializeField] private Button closeMessageButton;

        private void Start()
        {
            // パネルを初期状態で非表示
            selectedISOPanel.SetActive(false);
            selectedDevicePanel.SetActive(false);
            progressPanel.SetActive(false);
            messagePanel.SetActive(false);
            
            // ボタンイベントの設定
            refreshISOsButton.onClick.AddListener(OnRefreshISOsClicked);
            refreshDevicesButton.onClick.AddListener(OnRefreshDevicesClicked);
            writeButton.onClick.AddListener(OnWriteClicked);
            closeMessageButton.onClick.AddListener(() => messagePanel.SetActive(false));
            
            // マネージャーからのイベントを購読
            ISOManager.Instance.OnISOFilesUpdated += UpdateISOList;
            ISOManager.Instance.OnUSBDevicesUpdated += UpdateUSBList;
            ISOManager.Instance.OnWriteProgressUpdated += UpdateProgress;
            ISOManager.Instance.OnWriteStarted += OnWriteStarted;
            ISOManager.Instance.OnWriteCompleted += OnWriteCompleted;
            ISOManager.Instance.OnWriteError += ShowError;
            
            // 初期データ読み込み
            ISOManager.Instance.RefreshISOFiles();
            ISOManager.Instance.RefreshUSBDevices();
        }

        private void OnDestroy()
        {
            // イベント購読の解除
            if (ISOManager.Instance != null)
            {
                ISOManager.Instance.OnISOFilesUpdated -= UpdateISOList;
                ISOManager.Instance.OnUSBDevicesUpdated -= UpdateUSBList;
                ISOManager.Instance.OnWriteProgressUpdated -= UpdateProgress;
                ISOManager.Instance.OnWriteStarted -= OnWriteStarted;
                ISOManager.Instance.OnWriteCompleted -= OnWriteCompleted;
                ISOManager.Instance.OnWriteError -= ShowError;
            }
        }

        private void UpdateISOList(List<ISOFile> isoFiles)
        {
            // 既存の項目をクリア
            foreach (Transform child in isoListContent)
            {
                Destroy(child.gameObject);
            }
            
            // ISOファイルごとに項目を生成
            foreach (var iso in isoFiles)
            {
                GameObject item = Instantiate(isoListItemPrefab, isoListContent);
                
                // リストアイテムのレイアウト設定
                ConfigureListItemLayout(item);
                
                // 項目のテキストを設定
                TextMeshProUGUI isoNameText = item.transform.Find("NameText").GetComponent<TextMeshProUGUI>();
                TextMeshProUGUI isoSizeText = item.transform.Find("SizeText").GetComponent<TextMeshProUGUI>();
                
                isoNameText.text = iso.name;
                isoSizeText.text = iso.size_formatted;
                
                // クリックイベントを設定
                Button button = item.GetComponent<Button>();
                button.onClick.AddListener(() => OnISOSelected(iso));
            }
            
            // 選択中のISOがあれば選択状態を更新
            if (ISOManager.Instance.SelectedISO != null)
            {
                UpdateSelectedISO();
            }
        }

        private void UpdateUSBList(List<USBDevice> usbDevices)
        {
            // 既存の項目をクリア
            foreach (Transform child in usbListContent)
            {
                Destroy(child.gameObject);
            }
            
            // USBデバイスごとに項目を生成
            foreach (var device in usbDevices)
            {
                GameObject item = Instantiate(usbListItemPrefab, usbListContent);
                
                // リストアイテムのレイアウト設定
                ConfigureListItemLayout(item);
                
                // 項目のテキストを設定
                TextMeshProUGUI deviceNameText = item.transform.Find("NameText").GetComponent<TextMeshProUGUI>();
                TextMeshProUGUI deviceSizeText = item.transform.Find("SizeText").GetComponent<TextMeshProUGUI>();
                
                deviceNameText.text = device.name;
                deviceSizeText.text = device.size;
                
                // クリックイベントを設定
                Button button = item.GetComponent<Button>();
                button.onClick.AddListener(() => OnDeviceSelected(device));
            }
            
            // 選択中のデバイスがあれば選択状態を更新
            if (ISOManager.Instance.SelectedDevice != null)
            {
                UpdateSelectedDevice();
            }
        }

        // リストアイテムのレイアウト設定
        private void ConfigureListItemLayout(GameObject item)
        {
            // LayoutElementコンポーネントがなければ追加
            LayoutElement layoutElement = item.GetComponent<LayoutElement>();
            if (layoutElement == null)
            {
                layoutElement = item.AddComponent<LayoutElement>();
            }
            
            // 優先高さを設定（Vertical Layout Groupと連携）
            layoutElement.preferredHeight = 60;
            layoutElement.flexibleWidth = 1;
            
            // ボタンのサイズを調整
            RectTransform rectTransform = item.GetComponent<RectTransform>();
            rectTransform.anchorMin = new Vector2(0, 0);
            rectTransform.anchorMax = new Vector2(1, 0);
            rectTransform.pivot = new Vector2(0.5f, 0);
        }

        private void OnISOSelected(ISOFile iso)
        {
            // ISOファイルを選択
            ISOManager.Instance.SelectISO(iso);
            UpdateSelectedISO();
        }

        private void OnDeviceSelected(USBDevice device)
        {
            // USBデバイスを選択
            ISOManager.Instance.SelectDevice(device);
            UpdateSelectedDevice();
        }

        private void UpdateSelectedISO()
        {
            // 選択中のISOファイル情報を表示
            var iso = ISOManager.Instance.SelectedISO;
            if (iso != null)
            {
                selectedISOPanel.SetActive(true);
                selectedISONameText.text = iso.name;
                selectedISOSizeText.text = iso.size_formatted;
            }
            else
            {
                selectedISOPanel.SetActive(false);
            }
            
            UpdateWriteButton();
        }

        private void UpdateSelectedDevice()
        {
            // 選択中のUSBデバイス情報を表示
            var device = ISOManager.Instance.SelectedDevice;
            if (device != null)
            {
                selectedDevicePanel.SetActive(true);
                selectedDeviceNameText.text = device.name;
                selectedDeviceSizeText.text = device.size;
            }
            else
            {
                selectedDevicePanel.SetActive(false);
            }
            
            UpdateWriteButton();
        }

        private void UpdateWriteButton()
        {
            // 書き込みボタンの有効/無効を更新
            writeButton.interactable = ISOManager.Instance.SelectedISO != null && 
                                      ISOManager.Instance.SelectedDevice != null &&
                                      !ISOManager.Instance.IsWriting;
        }

        private void OnRefreshISOsClicked()
        {
            ISOManager.Instance.RefreshISOFiles();
        }

        private void OnRefreshDevicesClicked()
        {
            ISOManager.Instance.RefreshUSBDevices();
        }

        private void OnWriteClicked()
        {
            ISOManager.Instance.StartWriting();
        }

        private void OnWriteStarted()
        {
            // 書き込み開始時のUI更新
            progressPanel.SetActive(true);
            progressSlider.value = 0;
            progressText.text = "0%";
            statusText.text = "Writing started...";
            
            // 書き込み中はボタンを無効化
            writeButton.interactable = false;
        }

        private void UpdateProgress(ProgressData progressData)
        {
            // 進捗情報の更新
            progressPanel.SetActive(true);
            progressSlider.value = progressData.progress / 100f;
            progressText.text = $"{progressData.progress}%";
            
            // 翻訳されたステータスメッセージを表示
            statusText.text = WebSocketClient.GetStatusMessage(progressData.status);
        }

        private void OnWriteCompleted()
        {
            // 書き込み完了時のUI更新
            ShowMessage("Write completed successfully!");
            
            // 少し遅延してから進捗パネルを非表示
            Invoke("HideProgressPanel", 3f);
            
            // デバイス一覧を更新
            ISOManager.Instance.RefreshUSBDevices();
            
            // 書き込みボタンの状態を更新
            UpdateWriteButton();
        }

        private void HideProgressPanel()
        {
            progressPanel.SetActive(false);
        }

        private void ShowError(string error)
        {
            ShowMessage($"Error: {error}");
            
            if (ISOManager.Instance.IsWriting)
            {
                // エラーが発生した場合は進捗パネルを非表示
                HideProgressPanel();
            }
            
            // 書き込みボタンの状態を更新
            UpdateWriteButton();
        }

        private void ShowMessage(string message)
        {
            messagePanel.SetActive(true);
            messageText.text = message;
        }
    }
}
