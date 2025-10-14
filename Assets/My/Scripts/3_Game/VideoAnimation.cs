using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

/// <summary> 비디오 등장 시 Y scale 0 -> 1, 사라질 때 alpha 1 -> 0 </summary>
public class VideoAnimation : MonoBehaviour
{
    [Header("References")] [SerializeField]
    private Image maskingImage; // 마스크 이미지

    [SerializeField] private RawImage targetRaw; // 자식 RawImage
    [SerializeField] private VideoPlayer videoPlayer;

    [Header("Timings")] [SerializeField] private float scaleDuration = 0.5f; // 스케일 애니메이션 시간
    [SerializeField] private float fadeDuration = 0.5f; // 알파 페이드 시간

    [Header("Options")] [SerializeField] private bool waitUntilPrepared = true; // 준비 완료 전까지 애니메이션 지연
    [SerializeField] private bool bakeFirstFrameOnDisable = true; // 비활성화 시 첫 프레임 고정

    private void Reset()
    {
        if (maskingImage == null) maskingImage = GetComponent<Image>();
        if (targetRaw == null) targetRaw = GetComponentInChildren<RawImage>(true);
        if (videoPlayer == null) videoPlayer = GetComponentInParent<VideoPlayer>(true);
    }

    private void Awake()
    {
        if (!maskingImage) Debug.LogError($"[VideoAnimation] {name}: maskingImage is null");
        if (!targetRaw) Debug.LogWarning($"[VideoAnimation] {name}: targetRaw is null");
        if (!videoPlayer) Debug.LogWarning($"[VideoAnimation] {name}: videoPlayer is null");
    }

    private void OnEnable()
    {
        // 초기 스케일 Y=0, 알파=1
        Vector3 localScale = transform.localScale;
        transform.localScale = new Vector3(localScale.x, 0f, localScale.z);

        if (maskingImage)
        {
            Color c = maskingImage.color;
            c.a = 1f;
            maskingImage.color = c;
        }

        StartCoroutine(PlayInSequence());
    }

    private IEnumerator PlayInSequence()
    {
        if (waitUntilPrepared && videoPlayer && targetRaw)
        {
            yield return EnsureFirstFrameReady();
        }

        yield return YScaleAnimation();
    }

    /// <summary> VideoPlayer를 준비시키고 첫 프레임을 targetRaw에 그려둔다. </summary>
    private IEnumerator EnsureFirstFrameReady()
    {
        if (!videoPlayer.isPrepared)
        {
            videoPlayer.Prepare();
            while (!videoPlayer.isPrepared)
                yield return null;
        }

        // frame=0으로 맞추고 강제로 한 번 그리기
        long targetFrame = 1;
        if (videoPlayer.canSetTime) videoPlayer.frame = targetFrame;

        // 강제 렌더 유도
        bool wasPlaying = videoPlayer.isPlaying;
        videoPlayer.Play();
        videoPlayer.Pause();

        yield return null;

        // RawImage에 텍스처 보장
        if (targetRaw && targetRaw.texture == null)
        {
            Texture videoTex = videoPlayer.texture;
            if (videoTex != null) targetRaw.texture = videoTex;
        }
    }

    private IEnumerator YScaleAnimation()
    {
        float elapsed = 0f;
        Vector3 start = transform.localScale;
        Vector3 target = new Vector3(start.x, 1f, start.z);

        while (elapsed < scaleDuration)
        {
            float t = elapsed / scaleDuration;
            transform.localScale = Vector3.Lerp(start, target, t);
            elapsed += Time.deltaTime;
            yield return null;
        }

        transform.localScale = target;
    }

    private IEnumerator FadeOutAlpha()
    {
        if (!maskingImage) yield break;

        float elapsed = 0f;
        Color start = maskingImage.color;
        Color target = new Color(start.r, start.g, start.b, 0f);

        while (elapsed < fadeDuration)
        {
            float t = elapsed / fadeDuration;
            maskingImage.color = Color.Lerp(start, target, t);
            elapsed += Time.deltaTime;
            yield return null;
        }

        maskingImage.color = target;
        gameObject.SetActive(false);
    }

    private void OnDisable()
    {
        if (!bakeFirstFrameOnDisable) return;
        if (!videoPlayer || !targetRaw) return;

        // 다음 활성화를 위해 첫 프레임을 고정해 둔다.
        if (!videoPlayer.isPrepared) videoPlayer.Prepare();

        // 코루틴을 쓰지 않고도 간단히 스냅샷 시도(프레임 하나 렌더를 유도)
        if (videoPlayer.canSetTime) videoPlayer.frame = 0;
        videoPlayer.Play();
        videoPlayer.Pause();

        if (targetRaw.texture == null && videoPlayer.texture != null)
        {
            targetRaw.texture = videoPlayer.texture;
        }
    }
}
