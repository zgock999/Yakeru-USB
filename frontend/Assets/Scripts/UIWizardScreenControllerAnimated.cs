using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace YakeruUSB
{
    /// <summary>
    /// アニメーション機能を実装したUIWizardScreenControllerの拡張クラス
    /// </summary>
    public class UIWizardScreenControllerAnimated : UIWizardScreenController
    {
        [Header("アニメーション設定")]
        [SerializeField] private AnimationType enterAnimationType = AnimationType.FadeIn;
        [SerializeField] private AnimationType exitAnimationType = AnimationType.FadeOut;
        [SerializeField] private float slideDuration = 0.3f;
        [SerializeField] private float slideDistance = 100f;
        [SerializeField] private bool enableAnimations = true;
        
        private enum AnimationType
        {
            None,
            FadeIn,
            FadeOut,
            SlideFromRight,
            SlideFromLeft,
            SlideToRight,
            SlideToLeft,
            ScaleUp,
            ScaleDown
        }
        
        // 現在実行中のアニメーションコルーチン
        private Coroutine currentEnterAnimation;
        private Coroutine currentExitAnimation;
        private bool isEnterComplete = false;
        private bool isExitComplete = false;
        
        // 画面終了時の処理をオーバーライド
        protected override bool OnScreenExit(GameObject screen)
        {
            if (!enableAnimations || screen == null)
                return true;
                
            if (currentExitAnimation == null)
            {
                isExitComplete = false;
                currentExitAnimation = StartCoroutine(PlayExitAnimation(screen));
            }
            
            return isExitComplete;
        }
        
        // 画面開始時の処理をオーバーライド
        protected override bool OnScreenEnter(GameObject screen)
        {
            if (!enableAnimations || screen == null)
                return true;
                
            if (currentEnterAnimation == null)
            {
                isEnterComplete = false;
                currentEnterAnimation = StartCoroutine(PlayEnterAnimation(screen));
            }
            
            return isEnterComplete;
        }
        
        // 退場アニメーションの実行
        private IEnumerator PlayExitAnimation(GameObject screen)
        {
            // 退場アニメーションのタイプに応じて処理
            switch (exitAnimationType)
            {
                case AnimationType.FadeOut:
                    yield return FadeOutAnimation(screen);
                    break;
                    
                case AnimationType.SlideToRight:
                    yield return SlideAnimation(screen, Vector2.right * slideDistance, slideDuration);
                    break;
                    
                case AnimationType.SlideToLeft:
                    yield return SlideAnimation(screen, Vector2.left * slideDistance, slideDuration);
                    break;
                    
                case AnimationType.ScaleDown:
                    yield return ScaleAnimation(screen, Vector3.one, Vector3.zero, slideDuration);
                    break;
                    
                // その他のアニメーションタイプも必要に応じて実装
                default:
                    // デフォルトの場合、即座に完了
                    break;
            }
            
            isExitComplete = true;
            currentExitAnimation = null;
        }
        
        // 登場アニメーションの実行
        private IEnumerator PlayEnterAnimation(GameObject screen)
        {
            // 初期状態の設定
            switch (enterAnimationType)
            {
                case AnimationType.FadeIn:
                    // FadeInAnimationの前処理
                    SetupFadeIn(screen);
                    break;
                    
                case AnimationType.SlideFromRight:
                    SetupSlideFrom(screen, Vector2.right * slideDistance);
                    break;
                    
                case AnimationType.SlideFromLeft:
                    SetupSlideFrom(screen, Vector2.left * slideDistance);
                    break;
                    
                case AnimationType.ScaleUp:
                    SetupScale(screen, Vector3.zero);
                    break;
                    
                // その他のアニメーションタイプも必要に応じて実装
            }
            
            // 登場アニメーションのタイプに応じて処理
            switch (enterAnimationType)
            {
                case AnimationType.FadeIn:
                    yield return FadeInAnimation(screen);
                    break;
                    
                case AnimationType.SlideFromRight:
                    yield return SlideAnimation(screen, Vector2.zero, slideDuration);
                    break;
                    
                case AnimationType.SlideFromLeft:
                    yield return SlideAnimation(screen, Vector2.zero, slideDuration);
                    break;
                    
                case AnimationType.ScaleUp:
                    yield return ScaleAnimation(screen, Vector3.zero, Vector3.one, slideDuration);
                    break;
                    
                // その他のアニメーションタイプも必要に応じて実装
                default:
                    // デフォルトの場合、即座に完了
                    break;
            }
            
            isEnterComplete = true;
            currentEnterAnimation = null;
        }
        
        private void SetupFadeIn(GameObject screen)
        {
            CanvasGroup canvasGroup = GetOrAddCanvasGroup(screen);
            canvasGroup.alpha = 0;
        }
        
        private void SetupSlideFrom(GameObject screen, Vector2 offset)
        {
            RectTransform rectTransform = screen.GetComponent<RectTransform>();
            if (rectTransform != null)
            {
                rectTransform.anchoredPosition = offset;
            }
        }
        
        private void SetupScale(GameObject screen, Vector3 startScale)
        {
            screen.transform.localScale = startScale;
        }
        
        private CanvasGroup GetOrAddCanvasGroup(GameObject obj)
        {
            CanvasGroup group = obj.GetComponent<CanvasGroup>();
            if (group == null)
            {
                group = obj.AddComponent<CanvasGroup>();
            }
            return group;
        }
        
        // スライドアニメーション
        private IEnumerator SlideAnimation(GameObject screen, Vector2 targetPosition, float duration)
        {
            RectTransform rectTransform = screen.GetComponent<RectTransform>();
            if (rectTransform == null)
                yield break;
                
            Vector2 startPosition = rectTransform.anchoredPosition;
            float startTime = Time.time;
            
            while (Time.time < startTime + duration)
            {
                float t = (Time.time - startTime) / duration;
                rectTransform.anchoredPosition = Vector2.Lerp(startPosition, targetPosition, Mathf.SmoothStep(0, 1, t));
                yield return null;
            }
            
            rectTransform.anchoredPosition = targetPosition;
        }
        
        // スケールアニメーション
        private IEnumerator ScaleAnimation(GameObject screen, Vector3 startScale, Vector3 targetScale, float duration)
        {
            float startTime = Time.time;
            
            while (Time.time < startTime + duration)
            {
                float t = (Time.time - startTime) / duration;
                screen.transform.localScale = Vector3.Lerp(startScale, targetScale, Mathf.SmoothStep(0, 1, t));
                yield return null;
            }
            
            screen.transform.localScale = targetScale;
        }
    }
}
