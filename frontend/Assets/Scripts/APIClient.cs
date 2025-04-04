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
        private static bool ApplicationIsQuitting = false;
        public static APIClient Instance
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
                    _instance = FindAnyObjectByType<APIClient>();
                    #else
                    _instance = FindAnyObjectByType<APIClient>();
                    #endif
                    
                    if (_instance == null && !ApplicationIsQuitting)
                    {
                        GameObject go = new GameObject("APIClient");
                        _instance = go.AddComponent<APIClient>();
                        
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
                // タイムアウトを増加
                request.timeout = 15;
                
                // デバッグログレベルでリクエスト情報を出力
                Debug.Log($"Requesting USB devices from: {url}");
                
                yield return request.SendWebRequest();

                // レスポンスステータスのデバッグ出力
                Debug.Log($"USB devices response: Code={request.responseCode}, Error={request.error}");
                
                if (request.responseCode == 401)
                {
                    Debug.LogError("Authentication error (401) when accessing API. Check server authentication settings.");
                    // 401エラーでも空リストを返して処理を継続
                    onSuccess?.Invoke(new List<USBDevice>());
                    yield break;
                }
                else if (request.responseCode == 423) // Locked - 書き込み中のロック状態
                {
                    Debug.Log("USB device enumeration blocked by backend (resource locked/writing in progress)");
                    // ロック状態の場合は空リストを返す
                    onSuccess?.Invoke(new List<USBDevice>());
                    yield break;
                }
                else if (request.result == UnityWebRequest.Result.Success)
                {
                    try
                    {
                        string json = request.downloadHandler.text;
                        
                        // レスポンスからデバイスブロック状態をチェック
                        if (json.Contains("\"blocked\"") && json.Contains("\"message\""))
                        {
                            // デバイス取得がブロックされている場合は空リストを返す
                            Debug.Log("Backend blocked USB device enumeration during write operation");
                            onSuccess?.Invoke(new List<USBDevice>());
                            yield break;
                        }
                        
                        USBDeviceResponse response = JsonConvert.DeserializeObject<USBDeviceResponse>(json);
                        onSuccess?.Invoke(response.devices);
                    }
                    catch (Exception e)
                    {
                        Debug.LogError($"Parse error in USB device response: {e.Message}");
                        onError?.Invoke($"Parse error: {e.Message}");
                    }
                }
                else
                {
                    // エラー内容をより詳細に
                    string errorDetails = $"Error: {request.error}. Response code: {request.responseCode}";
                    if (!string.IsNullOrEmpty(request.downloadHandler?.text))
                    {
                        errorDetails += $". Response body: {request.downloadHandler.text}";
                    }
                    
                    Debug.LogError(errorDetails);
                    onError?.Invoke(errorDetails);
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
            
            // デバッグ情報を出力
            Debug.Log($"Sending write request to {url} with ISO: {isoFileName}, Device: {deviceId}");
            
            using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
            {
                byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);
                request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                
                // タイムアウトを増やす
                request.timeout = 30;
                
                yield return request.SendWebRequest();

                // レスポンスをより詳細に記録
                Debug.Log($"Write request response: Status={request.responseCode}, Error={request.error}, Body={request.downloadHandler?.text}");
                
                if (request.result != UnityWebRequest.Result.Success)
                {
                    string errorMessage = $"Error {request.responseCode}: {request.error}";
                    if (!string.IsNullOrEmpty(request.downloadHandler?.text))
                    {
                        // JSON応答からエラーメッセージを抽出
                        try {
                            WriteResponse errorResponse = JsonConvert.DeserializeObject<WriteResponse>(request.downloadHandler.text);
                            if (!string.IsNullOrEmpty(errorResponse.error))
                            {
                                errorMessage = errorResponse.error;
                            }
                        } catch {}
                    }
                    
                    onError?.Invoke(errorMessage);
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
                        Debug.LogException(e);
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
                request.timeout = 15; // タイムアウトの延長
                
                Debug.Log($"Requesting USB device rescan from: {url}");
                
                yield return request.SendWebRequest();

                // レスポンスステータスのデバッグ出力
                Debug.Log($"USB rescan response: Code={request.responseCode}, Error={request.error}");
                
                if (request.responseCode == 423) // 書き込み中のロック状態
                {
                    Debug.Log("USB device rescan blocked by backend (resource locked/writing in progress)");
                    // ロック状態の場合は空リストを返す
                    onSuccess?.Invoke(new List<USBDevice>());
                    yield break;
                }
                else if (request.result == UnityWebRequest.Result.Success)
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
                else
                {
                    // エラー内容をより詳細に
                    string errorDetails = $"Error: {request.error}. Response code: {request.responseCode}";
                    if (!string.IsNullOrEmpty(request.downloadHandler?.text))
                    {
                        errorDetails += $". Response body: {request.downloadHandler.text}";
                    }
                    
                    Debug.LogError(errorDetails);
                    onError?.Invoke(errorDetails);
                }
            }
        }

        // デバイス書き込み前の準備を行うAPI（Linuxのみ）
        public bool HasPrepareDeviceAPI()
        {
            // この機能がバックエンドに実装されているかをチェック
            // 現在のバージョンでは実装されていない場合は false を返す
            return false; // 実装されたらtrueに変更
        }
        
        // デバイスを書き込み前に準備するAPI（マウント状態の強制解除など）
        public IEnumerator PrepareDevice(string deviceId, Action<bool> onSuccess, Action<string> onError)
        {
            string url = $"{apiBaseUrl}/prepare-device/{deviceId}";
            
            using (UnityWebRequest request = UnityWebRequest.Get(url))
            {
                request.timeout = 30; // デバイス準備は時間がかかる可能性があるため、タイムアウトを長めに
                
                yield return request.SendWebRequest();
                
                if (request.result == UnityWebRequest.Result.Success)
                {
                    try
                    {
                        string json = request.downloadHandler.text;
                        var response = JsonConvert.DeserializeObject<Dictionary<string, object>>(json);
                        
                        if (response.ContainsKey("success") && (bool)response["success"])
                        {
                            onSuccess?.Invoke(true);
                        }
                        else
                        {
                            string error = response.ContainsKey("error") ? response["error"].ToString() : "Unknown error";
                            onSuccess?.Invoke(false); // 失敗してもとりあえず続行するため成功を返す
                            onError?.Invoke(error);
                        }
                    }
                    catch (Exception e)
                    {
                        Debug.LogError($"Error parsing prepare device response: {e.Message}");
                        onSuccess?.Invoke(false);
                        onError?.Invoke($"Failed to parse response: {e.Message}");
                    }
                }
                else
                {
                    // APIが実装されていない場合もここに来る可能性がある
                    Debug.LogWarning($"Prepare device API request failed: {request.error}");
                    onSuccess?.Invoke(false);
                    onError?.Invoke(request.error);
                }
            }
        }

        // バックエンドの状態をリセットするメソッドを追加
        public IEnumerator ResetBackendStatus(Action onSuccess = null, Action<string> onError = null)
        {
            string url = $"{apiBaseUrl}/reset-status";
            
            using (UnityWebRequest request = UnityWebRequest.PostWwwForm(url, ""))
            {
                yield return request.SendWebRequest();
                
                if (request.result == UnityWebRequest.Result.Success)
                {
                    Debug.Log("Backend state reset successfully");
                    onSuccess?.Invoke();
                }
                else
                {
                    Debug.LogWarning($"Failed to reset backend state: {request.error}");
                    onError?.Invoke(request.error);
                }
            }
        }

        // APIサーバーの状態をテストするメソッドを追加
        public IEnumerator TestAPIConnection(Action<bool, string> onComplete)
        {
            string url = $"{apiBaseUrl}/health";  // または存在するエンドポイント
            
            using (UnityWebRequest request = UnityWebRequest.Get(url))
            {
                request.timeout = 5;
                yield return request.SendWebRequest();
                
                bool isSuccess = request.result == UnityWebRequest.Result.Success;
                string message = isSuccess ? 
                    "API server connection successful" : 
                    $"API connection error: {request.error} (Code: {request.responseCode})";
                    
                onComplete?.Invoke(isSuccess, message);
            }
        }

        // 書き込み状態を取得するメソッドを追加（ISOManagerから呼び出される）
        public IEnumerator GetWriteStatus(Action<ProgressData> onSuccess, Action<string> onError)
        {
            string url = $"{apiBaseUrl}/write-status";
            
            using (UnityWebRequest request = UnityWebRequest.Get(url))
            {
                yield return request.SendWebRequest();
                
                if (request.result == UnityWebRequest.Result.Success)
                {
                    try
                    {
                        string json = request.downloadHandler.text;
                        var response = JsonConvert.DeserializeObject<ProgressData>(json);
                        onSuccess?.Invoke(response);
                    }
                    catch (Exception e)
                    {
                        Debug.LogError($"Error parsing write status: {e.Message}");
                        onError?.Invoke($"Failed to parse response: {e.Message}");
                    }
                }
                else
                {
                    Debug.LogWarning($"Failed to get write status: {request.error}");
                    onError?.Invoke(request.error);
                }
            }
        }
    }
}
