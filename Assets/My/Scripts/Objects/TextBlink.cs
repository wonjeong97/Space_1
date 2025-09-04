using UnityEngine;
using TMPro;
using System.Collections;

public class TextBlink : MonoBehaviour
{
    private float periodSeconds = 3f; // 한 사이클(밝아졌다 어두워짐) 시간
    private readonly int minAlpha255 = 0;
    private readonly int maxAlpha255 = 255;

    private TMP_Text tmp;
    private Coroutine routine;

    private void Awake()
    {
        // 자기 자신에서 우선 검색, 없으면 자식에서 한 번만 검색
        if (!TryGetComponent(out tmp))
            tmp = GetComponentInChildren<TMP_Text>(includeInactive: true);
    }

    private void OnEnable()
    {
        if (tmp == null)
        {
            Debug.LogWarning("[TextBlink] TMP_Text not found on this GameObject or its children.");
            return;
        }

        routine = StartCoroutine(BlinkRoutine());
    }

    private void OnDisable()
    {
        if (routine != null)
        {
            StopCoroutine(routine);
            routine = null;
        }
        // 필요 시 비활성화 시점에 최대 알파로 복구하려면 아래 주석을 해제
        // SetAlpha01(maxAlpha255 / 255f);
    }

    private IEnumerator BlinkRoutine()
    {
        periodSeconds = Mathf.Max(0.0001f, periodSeconds);
        float min01 = Mathf.Clamp01(minAlpha255 / 255f);
        float max01 = Mathf.Clamp01(maxAlpha255 / 255f);

        float t = 0f;
        while (true)
        {
            t += Time.deltaTime;
            float ping = Mathf.PingPong(t * (2f / periodSeconds), 1f); // periodSeconds에 맞춘 0~1~0
            float a = Mathf.Lerp(min01, max01, ping);
            SetAlpha01(a);
            yield return null;
        }
    }

    private void SetAlpha01(float a01)
    {
        // TMP_Text.alpha는 최종 렌더 알파에 곱해지는 배율
        tmp.alpha = a01;
    }
}