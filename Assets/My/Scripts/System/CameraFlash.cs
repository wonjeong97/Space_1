using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class CameraFlash : MonoBehaviour
{
    public static CameraFlash Instance { get; private set; }

    [SerializeField] private Image flashImage;
    private readonly float flashDuration = 1f;
    private float flashAlpha = 0.7f;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        if (!flashImage)
        {
            Debug.LogError("[CameraFlash] FlashImage is null]");
        }
        else
        {
            SetAlpha(0.0f);
            flashImage.raycastTarget = false;
        }
    }

    public void Flash()
    {
        StartCoroutine(FlashImage());
    }

    private IEnumerator FlashImage()
    {
        if (!flashImage) yield break;
        SetAlpha(flashAlpha);
        flashImage.transform.SetAsLastSibling();

        float t = 0f;

        Color baseColor = flashImage.color;

        while (t < flashDuration)
        {
            t += Time.deltaTime;
            float a = Mathf.Lerp(flashAlpha, 0f, t / flashDuration);
            flashImage.color = new Color(baseColor.r, baseColor.g, baseColor.b, a);
            yield return null;
        }

        SetAlpha(0.0f);
        flashImage.transform.SetAsFirstSibling();
    }

    private void SetAlpha(float alpha)
    {
        if (!flashImage) return;
        var c1 = flashImage.color;
        flashImage.color = new Color(c1.r, c1.g, c1.b, alpha);
    }
}