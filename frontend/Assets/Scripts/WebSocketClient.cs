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
    }

    public class WebSocketClient : MonoBehaviour
    {
        [SerializeField] private string socketUrl = "http://localhost:5000";
        [SerializeField] private float pollInterval = 1.0f; // 進捗確認の間隔（秒）
        
        private bool isConnected = false;
        private bool isPolling = false;

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

        private void OnDestroy()
        {
            Disconnect();
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
                yield return request.SendWebRequest();

                if (request.result == UnityWebRequest.Result.Success)
                {
                    try
                    {
                        string json = request.downloadHandler.text;
                        JObject responseObj = JObject.Parse(json);

                        if (responseObj.ContainsKey("progress"))
                        {
                            ProgressData progress = new ProgressData
                            {
                                progress = responseObj["progress"].Value<int>(),
                                status = responseObj["status"].Value<string>()
                            };

                            // イベント発火
                            OnProgressUpdated?.Invoke(progress);
                        }
                    }
                    catch (Exception e)
                    {
                        Debug.LogError($"Error parsing progress data: {e.Message}");
                    }
                }
            }
        }

        public void Disconnect()
        {
            isConnected = false;
            isPolling = false;
            StopAllCoroutines();
        }

        // 直接進捗データを受け取るためのメソッド（テスト用）
        public void SimulateProgressUpdate(int progress, string status)
        {
            ProgressData progressData = new ProgressData
            {
                progress = progress,
                status = status
            };
            
            OnProgressUpdated?.Invoke(progressData);
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
