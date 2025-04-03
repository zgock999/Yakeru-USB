using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using Newtonsoft.Json;

namespace YakeruUSB
{
    [Serializable]
    public class ISOFile
    {
        public string name;
        public long size;
        public string size_formatted;
        public string path;
    }

    [Serializable]
    public class ISOFileResponse
    {
        public List<ISOFile> isos;
    }

    [Serializable]
    public class USBDevice
    {
        public string id;
        public string name;
        public string size;
        public string vendor;
        public string status;
        public string mountpoint;
    }

    [Serializable]
    public class USBDeviceResponse
    {
        public List<USBDevice> devices;
    }

    [Serializable]
    public class WriteRequest
    {
        public string iso_file;
        public string device;
    }

    [Serializable]
    public class WriteResponse
    {
        public string status;
        public string error;
    }

    public class APIClient : MonoBehaviour
    {
        [SerializeField] private string apiBaseUrl = "http://localhost:5000/api";
        
        // シングルトンインスタンス
        private static APIClient _instance;
        public static APIClient Instance
        {
            get
            {
                if (_instance == null)
                {
                    #if UNITY_2022_3_OR_NEWER
                    _instance = FindAnyObjectByType<APIClient>();
                    #else
                    _instance = FindObjectOfType<APIClient>();
                    #endif
                    
                    if (_instance == null)
                    {
                        GameObject go = new GameObject("APIClient");
                        _instance = go.AddComponent<APIClient>();
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
        }

        private void OnDestroy()
        {
            // このインスタンスが現在のシングルトンインスタンスであれば、参照をクリア
            if (_instance == this)
            {
                _instance = null;
            }
        }

        // ISOファイル一覧を取得
        public IEnumerator GetISOFiles(Action<List<ISOFile>> onSuccess, Action<string> onError)
        {
            string url = $"{apiBaseUrl}/isos";
            
            using (UnityWebRequest request = UnityWebRequest.Get(url))
            {
                yield return request.SendWebRequest();

                if (request.result != UnityWebRequest.Result.Success)
                {
                    onError?.Invoke($"Error: {request.error}");
                }
                else
                {
                    try
                    {
                        string json = request.downloadHandler.text;
                        ISOFileResponse response = JsonConvert.DeserializeObject<ISOFileResponse>(json);
                        onSuccess?.Invoke(response.isos);
                    }
                    catch (Exception e)
                    {
                        onError?.Invoke($"Parse error: {e.Message}");
                    }
                }
            }
        }

        // USBデバイス一覧を取得
        public IEnumerator GetUSBDevices(Action<List<USBDevice>> onSuccess, Action<string> onError)
        {
            string url = $"{apiBaseUrl}/usb-devices";
            
            using (UnityWebRequest request = UnityWebRequest.Get(url))
            {
                yield return request.SendWebRequest();

                if (request.result != UnityWebRequest.Result.Success)
                {
                    onError?.Invoke($"Error: {request.error}");
                }
                else
                {
                    try
                    {
                        string json = request.downloadHandler.text;
                        USBDeviceResponse response = JsonConvert.DeserializeObject<USBDeviceResponse>(json);
                        onSuccess?.Invoke(response.devices);
                    }
                    catch (Exception e)
                    {
                        onError?.Invoke($"Parse error: {e.Message}");
                    }
                }
            }
        }

        // ISO書き込み開始リクエスト
        public IEnumerator WriteISOToDevice(string isoFileName, string deviceId, Action<string> onSuccess, Action<string> onError)
        {
            string url = $"{apiBaseUrl}/write";
            
            WriteRequest writeRequest = new WriteRequest
            {
                iso_file = isoFileName,
                device = deviceId
            };

            string jsonBody = JsonConvert.SerializeObject(writeRequest);
            
            using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
            {
                byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);
                request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                
                yield return request.SendWebRequest();

                if (request.result != UnityWebRequest.Result.Success)
                {
                    onError?.Invoke($"Error: {request.error}");
                }
                else
                {
                    try
                    {
                        string json = request.downloadHandler.text;
                        WriteResponse response = JsonConvert.DeserializeObject<WriteResponse>(json);
                        
                        if (!string.IsNullOrEmpty(response.error))
                        {
                            onError?.Invoke(response.error);
                        }
                        else
                        {
                            onSuccess?.Invoke(response.status);
                        }
                    }
                    catch (Exception e)
                    {
                        onError?.Invoke($"Parse error: {e.Message}");
                    }
                }
            }
        }

        // USBデバイスを強制的に再スキャン（Linux環境用）
        public IEnumerator RescanUSBDevices(Action<List<USBDevice>> onSuccess, Action<string> onError)
        {
            string url = $"{apiBaseUrl}/rescan-usb";
            
            using (UnityWebRequest request = UnityWebRequest.PostWwwForm(url, ""))
            {
                yield return request.SendWebRequest();

                if (request.result != UnityWebRequest.Result.Success)
                {
                    onError?.Invoke($"Error: {request.error}");
                }
                else
                {
                    try
                    {
                        string json = request.downloadHandler.text;
                        USBDeviceResponse response = JsonConvert.DeserializeObject<USBDeviceResponse>(json);
                        onSuccess?.Invoke(response.devices);
                    }
                    catch (Exception e)
                    {
                        onError?.Invoke($"Parse error: {e.Message}");
                    }
                }
            }
        }
    }
}
