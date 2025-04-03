using System;
using UnityEngine;

namespace YakeruUSB
{
    /// <summary>
    /// MonoBehaviour継承型シングルトンの基底クラス
    /// </summary>
    /// <typeparam name="T">シングルトンとして実装するクラス型</typeparam>
    public abstract class SingletonBase<T> : MonoBehaviour where T : MonoBehaviour
    {
        private static T _instance;
        private static bool _isQuitting = false;
        
        /// <summary>
        /// シングルトンインスタンスへのアクセサ
        /// </summary>
        public static T Instance
        {
            get
            {
                // アプリケーション終了中なら無視
                if (_isQuitting)
                {
                    return null;
                }
                
                // インスタンスがまだ作成されていない場合
                if (_instance == null)
                {
                    // 既存のインスタンスを探す
                    #if UNITY_2022_3_OR_NEWER
                    _instance = FindAnyObjectByType<T>();
                    #else
                    _instance = FindObjectOfType<T>();
                    #endif
                    
                    // 既存のインスタンスがない場合は新規作成
                    if (_instance == null)
                    {
                        GameObject singletonObject = new GameObject();
                        _instance = singletonObject.AddComponent<T>();
                        singletonObject.name = typeof(T).ToString() + " (Singleton)";
                        
                        DontDestroyOnLoad(singletonObject);
                    }
                }
                
                return _instance;
            }
        }
        
        /// <summary>
        /// Awakeで初期化処理を行う
        /// </summary>
        protected virtual void Awake()
        {
            InitializeSingleton();
        }
        
        /// <summary>
        /// シングルトンの初期化処理
        /// </summary>
        protected void InitializeSingleton()
        {
            if (_instance == null)
            {
                _instance = this as T;
                DontDestroyOnLoad(gameObject);
            }
            else if (_instance != this)
            {
                Destroy(gameObject);
            }
        }
        
        /// <summary>
        /// アプリケーション終了時の処理
        /// </summary>
        protected virtual void OnApplicationQuit()
        {
            _isQuitting = true;
            _instance = null;
        }
        
        /// <summary>
        /// オブジェクト破棄時の処理
        /// </summary>
        protected virtual void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }
    }
}
