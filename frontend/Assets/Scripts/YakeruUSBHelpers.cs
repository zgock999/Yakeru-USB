using UnityEngine;

namespace YakeruUSB
{
    /// <summary>
    /// アプリケーション全体で使用するヘルパーメソッドを提供するユーティリティクラス
    /// </summary>
    public static class YakeruUSBHelpers
    {
        /// <summary>
        /// Unity 2022.3以降のバージョンで非推奨警告を回避するためのObjectHelper
        /// </summary>
        public static class ObjectHelper
        {
            /// <summary>
            /// 指定した型のオブジェクトをシーン内から検索（任意のインスタンス - 高速）
            /// </summary>
            public static T FindAnyObjectOfType<T>() where T : Object
            {
                #if UNITY_2022_3_OR_NEWER
                return Object.FindAnyObjectByType<T>();
                #else
                return Object.FindObjectOfType<T>();
                #endif
            }

            /// <summary>
            /// 指定した型のオブジェクトをシーン内から検索（最初のインスタンス - 一貫性あり）
            /// </summary>
            public static T FindFirstObjectOfType<T>() where T : Object
            {
                #if UNITY_2022_3_OR_NEWER
                return Object.FindFirstObjectByType<T>();
                #else
                return Object.FindObjectOfType<T>();
                #endif
            }
            
            /// <summary>
            /// 指定した型の全オブジェクトをシーン内から検索
            /// </summary>
            public static T[] FindObjectsOfType<T>() where T : Object
            {
                #if UNITY_2022_3_OR_NEWER
                return Object.FindObjectsByType<T>(FindObjectsSortMode.None);
                #else
                return Object.FindObjectsOfType<T>();
                #endif
            }
        }
    }
}
