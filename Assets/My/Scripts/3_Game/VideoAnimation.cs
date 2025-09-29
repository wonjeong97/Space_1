using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary> 비디오 등장 시 Y scale 0 -> 1, 사라질 때 alpha 1 -> 0 </summary>
public class VideoAnimation : MonoBehaviour
{
    [SerializeField] private Image maskingImage;
    [SerializeField] private float scaleDuration = 0.5f; // 스케일 애니메이션 시간
    [SerializeField] private float fadeDuration = 0.5f;  // 알파 페이드 시간
    
    private RawImage rawImage;

    private void Awake()
    {
        if (!maskingImage)
        {
            Debug.LogError($"[VideoAnimation] {gameObject.name}: maskingImage not found");
        }
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
        
        StartCoroutine(YScaleAnimation());
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
}
