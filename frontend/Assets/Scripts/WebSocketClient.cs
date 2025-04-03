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
                    #if UNITY_2022_3_OR_NEWER
                    _instance = FindAnyObjectByType<WebSocketClient>();
                    #else
                    _instance = FindObjectOfType<WebSocketClient>();
                    #endif
                    
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

        private void OnApplicationQuit()
        {
            // アプリケーション終了時にシングルトンインスタンスをクリア
            _instance = null;
            
            // 接続を切断
            Disconnect();
        }

        private void OnDestroy()
        {
            Disconnect();
            
            // このインスタンスが現在のシングルトンインスタンスであれば、参照をクリア
            if (_instance == this)
            {
                _instance = null;
            }
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

        // ステータスメッセージの変換（フロントエンド表示用）
        public static string GetStatusMessage(string status)
        {
            switch (status)
            {
                case "started": return "準備中...";
                case "opening_device": return "デバイスを開いています...";
                case "locking_volume": return "ボリュームをロック中...";
                case "dismounting_volume": return "ボリュームをアンマウント中...";
                case "writing": return "書き込み中...";
                case "flushing": return "データをフラッシュ中...";
                case "completed": return "完了しました";
                default:
                    if (status.StartsWith("checking_volumes")) return "ボリュームを確認中...";
                    if (status.StartsWith("dismounting_")) return "ドライブをアンマウント中...";
                    if (status.StartsWith("error")) return status;
                    return status;
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
                    #if UNITY_2022_3_OR_NEWER
                    _instance = FindAnyObjectByType<MainThreadDispatcher>();
                    #else
                    _instance = FindObjectOfType<MainThreadDispatcher>();
                    #endif
                    
                    if (_instance == null)
                    {
                        var go = new GameObject("MainThreadDispatcher");
                        _instance = go.AddComponent<MainThreadDispatcher>();
                        DontDestroyOnLoad(go);
                    }
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
            else if (_instance != this)
            {
                Destroy(gameObject);
            }
        }

        private void OnApplicationQuit()
        {
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
