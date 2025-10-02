using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

public class FadeManager : MonoBehaviour
{
    public static FadeManager Instance { get; private set; }

    [SerializeField] private Image mainFadeImage;
    [SerializeField] private Image subFadeImage;

    // 실행 중인 페이드 취소용 CTS
    private CancellationTokenSource _bothFadeCts;
    private CancellationTokenSource _mainFadeCts;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        if (!mainFadeImage || !subFadeImage)
        {
            Debug.LogError("[FadeManager] Fade Image is not assigned.");
            return;
        }

        SetAlpha(1f); // 시작 시 둘 다 어둡게
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;

        _bothFadeCts?.Cancel();
        _bothFadeCts?.Dispose();
        _bothFadeCts = null;

        _mainFadeCts?.Cancel();
        _mainFadeCts?.Dispose();
        _mainFadeCts = null;
    }

    // ====================== Public APIs (both: main+sub) ======================

    public UniTask FadeInAsync(float duration, bool unscaledTime = false, CancellationToken external = default)
        => RunBothFadeAsync(1f, 0f, duration, unscaledTime, external);

    public UniTask FadeOutAsync(float duration, bool unscaledTime = false, CancellationToken external = default)
        => RunBothFadeAsync(0f, 1f, duration, unscaledTime, external);

    // ====================== Public APIs (single: main only) ======================

    public UniTask FadeInMainAsync(float duration, bool unscaledTime = false, CancellationToken external = default)
        => RunSingleFadeAsync(mainFadeImage, 1f, 0f, duration, unscaledTime, external);

    public UniTask FadeOutMainAsync(float duration, bool unscaledTime = false, CancellationToken external = default)
        => RunSingleFadeAsync(mainFadeImage, 0f, 1f, duration, unscaledTime, external);

    // ====================== Core fade (both) ======================

    private async UniTask RunBothFadeAsync(float from, float to, float duration, bool unscaled, CancellationToken external)
    {
        if (!mainFadeImage || !subFadeImage) return;

        // 이전 both 페이드 취소
        _bothFadeCts?.Cancel();
        _bothFadeCts?.Dispose();

        _bothFadeCts = CancellationTokenSource.CreateLinkedTokenSource(external);
        CancellationToken token = _bothFadeCts.Token;

        // 입력 차단 및 형제 순서 조정
        mainFadeImage.raycastTarget = true;
        subFadeImage.raycastTarget = true;

        mainFadeImage.transform.SetAsLastSibling();
        subFadeImage.transform.SetAsLastSibling();

        // 시작 알파 적용
        SetAlpha(from);

        try
        {
            float elapsed = 0f;
            float invDuration = Mathf.Approximately(duration, 0f) ? 0f : 1f / duration;

            while (elapsed < duration)
            {
                token.ThrowIfCancellationRequested();

                float dt = unscaled ? Time.unscaledDeltaTime : Time.deltaTime;
                elapsed += dt;

                float t = (duration <= 0f) ? 1f : Mathf.Clamp01(elapsed * invDuration);
                float alpha = Mathf.Lerp(from, to, t);
                SetAlpha(alpha);

                await UniTask.Yield(PlayerLoopTiming.Update, token);
            }

            // 최종 알파 스냅
            SetAlpha(to);
        }
        catch (OperationCanceledException)
        {
            // 취소 시 현재 알파 유지 후 종료
            return;
        }
        finally
        {
            // 완전 투명으로 들어갔을 때만 입력 허용/형제 순서 복귀
            if (to <= 0.001f)
            {
                mainFadeImage.raycastTarget = false;
                subFadeImage.raycastTarget = false;

                mainFadeImage.transform.SetAsFirstSibling(); // 기존 로직 유지
                subFadeImage.transform.SetAsLastSibling();   // 기존 로직 유지
            }
        }
    }

    // ====================== Core fade (single) ======================

    private async UniTask RunSingleFadeAsync(Image target, float from, float to, float duration, bool unscaled, CancellationToken external)
    {
        if (!target)
        {
            Debug.LogWarning("[FadeManager] Target Image is null for single fade.");
            return;
        }

        // 이전 main 단일 페이드 취소
        if (target == mainFadeImage)
        {
            _mainFadeCts?.Cancel();
            _mainFadeCts?.Dispose();
            _mainFadeCts = CancellationTokenSource.CreateLinkedTokenSource(external);
        }

        CancellationToken token = (target == mainFadeImage && _mainFadeCts != null)
            ? _mainFadeCts.Token
            : external;

        target.raycastTarget = true;
        target.transform.SetAsLastSibling();

        // 시작 알파 적용
        SetAlpha(target, from);

        try
        {
            float elapsed = 0f;
            float invDuration = Mathf.Approximately(duration, 0f) ? 0f : 1f / duration;

            while (elapsed < duration)
            {
                token.ThrowIfCancellationRequested();

                float dt = unscaled ? Time.unscaledDeltaTime : Time.deltaTime;
                elapsed += dt;

                float t = (duration <= 0f) ? 1f : Mathf.Clamp01(elapsed * invDuration);
                float alpha = Mathf.Lerp(from, to, t);
                SetAlpha(target, alpha);

                await UniTask.Yield(PlayerLoopTiming.Update, token);
            }

            SetAlpha(target, to);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        finally
        {
            if (to <= 0.001f)
            {
                target.raycastTarget = false;

                // 기존 규칙: main은 FirstSibling, 그 외는 LastSibling
                if (target == mainFadeImage)
                    target.transform.SetAsFirstSibling();
                else
                    target.transform.SetAsLastSibling();
            }
        }
    }

    // ====================== Helpers ======================

    private void SetAlpha(float alpha)
    {
        if (!mainFadeImage || !subFadeImage) return;

        Color c1 = mainFadeImage.color;
        mainFadeImage.color = new Color(c1.r, c1.g, c1.b, alpha);

        Color c2 = subFadeImage.color;
        subFadeImage.color = new Color(c2.r, c2.g, c2.b, alpha);
    }

    private void SetAlpha(Image target, float alpha)
    {
        if (!target) return;
        Color c = target.color;
        target.color = new Color(c.r, c.g, c.b, alpha);
    }
}