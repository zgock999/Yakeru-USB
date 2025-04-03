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
        public static ISOManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    #if UNITY_2022_3_OR_NEWER
                    _instance = FindAnyObjectByType<ISOManager>();
                    #else
                    _instance = FindObjectOfType<ISOManager>();
                    #endif
                    
                    if (_instance == null)
                    {
                        GameObject go = new GameObject("ISOManager");
                        _instance = go.AddComponent<ISOManager>();
                        DontDestroyOnLoad(go);
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
            DontDestroyOnLoad(gameObject);
        }

        private void OnApplicationQuit()
        {
            // アプリケーション終了時にシングルトンインスタンスをクリア
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
            if (WebSocketClient.Instance != null)
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
            // WebSocketのイベントを購読
            WebSocketClient.Instance.OnProgressUpdated += HandleProgressUpdate;
            WebSocketClient.Instance.Connect();
            
            // 定期的に一覧を更新
            StartCoroutine(RefreshDataRoutine());
        }

        private IEnumerator RefreshDataRoutine()
        {
            while (true)
            {
                RefreshISOFiles();
                RefreshUSBDevices();
                
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
            // プラットフォームに応じた処理
            if (Application.platform == RuntimePlatform.LinuxEditor || 
                Application.platform == RuntimePlatform.LinuxPlayer)
            {
                // Linux環境では強制的な再スキャンを試行
                StartCoroutine(APIClient.Instance.RescanUSBDevices(
                    onSuccess: devices =>
                    {
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
            StartCoroutine(APIClient.Instance.GetUSBDevices(
                onSuccess: devices =>
                {
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

            StartCoroutine(APIClient.Instance.WriteISOToDevice(
                SelectedISO.name,
                SelectedDevice.id,
                onSuccess: status =>
                {
                    isWriting = true;
                    OnWriteStarted?.Invoke();
                    
                    // WebSocket接続を開始または再接続
                    WebSocketClient.Instance.Connect();
                },
                onError: error =>
                {
                    OnWriteError?.Invoke($"Failed to start writing: {error}");
                }
            ));
        }

        // 進捗更新のハンドラ
        private void HandleProgressUpdate(ProgressData progressData)
        {
            OnWriteProgressUpdated?.Invoke(progressData);
            
            if (progressData.status == "completed")
            {
                isWriting = false;
                OnWriteCompleted?.Invoke();
            }
            else if (progressData.status.StartsWith("error"))
            {
                isWriting = false;
                OnWriteError?.Invoke(progressData.status);
            }
        }
    }
}
