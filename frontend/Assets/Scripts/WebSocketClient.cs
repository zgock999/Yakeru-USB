using System;
using System.Collections;
using UnityEngine;
using NativeWebSocket;
using Newtonsoft.Json;

namespace YakeruUSB
{
    [Serializable]
    public class ProgressData
    {
        public int progress;
        public string status;
    }

    public class WebSocketClient : MonoBehaviour
    {
        [SerializeField] private string webSocketUrl = "ws://localhost:5000";
        
        private WebSocket webSocket;
        private bool isConnected = false;

        // シングルトンインスタンス
        private static WebSocketClient _instance;
        public static WebSocketClient Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindObjectOfType<WebSocketClient>();
                    if (_instance == null)
                    {
                        GameObject go = new GameObject("WebSocketClient");
                        _instance = go.AddComponent<WebSocketClient>();
                        DontDestroyOnLoad(go);
                    }
                }
                return _instance;
            }
        }

        // 進捗更新イベント
        public event Action<ProgressData> OnProgressUpdated;

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

        private async void OnDestroy()
        {
            if (webSocket != null)
            {
                await webSocket.Close();
            }
        }

        private void Update()
        {
            if (webSocket != null)
            {
                #if !UNITY_WEBGL || UNITY_EDITOR
                webSocket.DispatchMessageQueue();
                #endif
            }
        }

        public void Connect()
        {
            if (isConnected)
            {
                return;
            }

            StartCoroutine(ConnectToWebSocket());
        }

        private IEnumerator ConnectToWebSocket()
        {
            webSocket = new WebSocket(webSocketUrl);

            webSocket.OnOpen += () =>
            {
                Debug.Log("WebSocket connection opened");
                isConnected = true;
            };

            webSocket.OnError += (e) =>
            {
                Debug.LogError($"WebSocket Error: {e}");
            };

            webSocket.OnClose += (e) =>
            {
                Debug.Log("WebSocket connection closed");
                isConnected = false;
            };

            webSocket.OnMessage += (bytes) =>
            {
                var message = System.Text.Encoding.UTF8.GetString(bytes);
                ProcessMessage(message);
            };

            // WebSocketに接続
            yield return new WaitForSeconds(0.5f);
            
            try
            {
                yield return webSocket.Connect();
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to connect to WebSocket: {e.Message}");
            }
        }

        private void ProcessMessage(string message)
        {
            try
            {
                // Socket.IOからのメッセージはJSON形式なので、適切にパース
                // 実際のSocket.IOメッセージ形式に応じて適宜調整が必要
                if (message.Contains("write_progress"))
                {
                    // Socket.IOメッセージからデータ部分を抽出
                    int startIndex = message.IndexOf("{\"progress\":");
                    if (startIndex >= 0)
                    {
                        string jsonPart = message.Substring(startIndex);
                        int endIndex = jsonPart.LastIndexOf("}");
                        if (endIndex >= 0)
                        {
                            jsonPart = jsonPart.Substring(0, endIndex + 1);
                            ProgressData progressData = JsonConvert.DeserializeObject<ProgressData>(jsonPart);
                            
                            // メインスレッドでイベント発火
                            MainThreadDispatcher.Execute(() =>
                            {
                                OnProgressUpdated?.Invoke(progressData);
                            });
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Error processing WebSocket message: {e.Message}");
            }
        }

        public async void Disconnect()
        {
            if (webSocket != null)
            {
                await webSocket.Close();
            }
        }
    }

    // メインスレッドでコールバックを実行するためのユーティリティクラス
    public class MainThreadDispatcher : MonoBehaviour
    {
        private static MainThreadDispatcher _instance;
        private readonly Queue<Action> _executionQueue = new Queue<Action>();

        public static MainThreadDispatcher Instance
        {
            get
            {
                if (_instance == null)
                {
                    var go = new GameObject("MainThreadDispatcher");
                    _instance = go.AddComponent<MainThreadDispatcher>();
                    DontDestroyOnLoad(go);
                }
                return _instance;
            }
        }

        private void Awake()
        {
            if (_instance == null)
            {
                _instance = this;
                DontDestroyOnLoad(this.gameObject);
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
    }

    // スレッド判定用の拡張メソッド
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
