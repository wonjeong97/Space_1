using System.Collections;
using UnityEngine;

/// <summary>
/// 마우스가 5초 이상 움직이지 않으면 커서 지점을 기준으로
/// 배경 RectTransform을 1회만(페이지당) 1초 동안 확대 후 원래 크기로 복귀.
/// </summary>
public class ZoomBackground : MonoBehaviour
{
    // Target
    private RectTransform rt;       // 배경 RectTransform
    private Canvas canvas;          // 상위 Canvas

    // Config
    private readonly Vector2 pivotOrigin = new Vector2(0f, 1f); // 기본 pivot(좌상단)
    private float zoomScale;        // 확대 배율
    private float zoomThreshold;    //  정지 시 발동 시간
    private float zoomDuration;    

    // State
    private bool isCaptured;        // 페이지당 1회만
    private bool isAnimating;
    private Vector3 lastMousePos;
    private float idleTimer;

    public int backgroundIndex;
    
    private void Awake()
    {
        rt = GetComponent<RectTransform>();
        canvas = GetComponentInParent<Canvas>();

        if (!rt || !canvas)
            Debug.LogError("[ZoomBackground] RectTransform or Canvas not found");

        lastMousePos = Input.mousePosition;
    }

    private void Start()
    {
        var settings = JsonLoader.Instance?.settings;
        // if (settings != null)
        // {   
        //     zoomThreshold = settings.zoomThreshold;
        //     zoomScale = settings.zoomScale;
        //     zoomDuration = settings.zoomDuration;
        // }
    }

    private void OnDisable()
    {
        // 페이지 전환/비활성화 시 상태 초기화
        isCaptured = false;
        isAnimating = false;
        idleTimer = 0f;
        StopAllCoroutines();

        if (rt)
        {
            rt.localScale = Vector3.one;
            SetPivotKeepTopLeft(rt, pivotOrigin);
        }
    }

    private void Update()
    {
        if (!rt || !canvas) return;

        // 마우스 정지 시간 측정
        if (Input.mousePosition != lastMousePos)
        {
            idleTimer = 0f;
            lastMousePos = Input.mousePosition;
        }
        else
        {
            idleTimer += Time.deltaTime;
        }

        // 설정한 시간만큼 정지 + 아직 캡처 안 함 + 애니메이션 중 아님 → 발동
        if (!isCaptured && !isAnimating && idleTimer >= zoomThreshold)
        {
            isCaptured = true;

            // 커서 위치를 기준으로 pivot 이동(보정 포함)
            Vector2 mouse = lastMousePos;
            Vector2 newPivot = ScreenPointToPivot01(rt, mouse, GetUICamera());
            SetPivotKeepTopLeft(rt, newPivot);

            // 1초 확대 후 원복
            StartCoroutine(ZoomPulse());
        }
    }

    private IEnumerator ZoomPulse()
    {
        isAnimating = true;

        float half = zoomDuration * 0.5f;
        float start = 1f;
        float end = Mathf.Max(zoomScale, 1.01f); // 안전 가드

        // Zoom In (0.5s)
        float t = 0f;
        while (t < half)
        {
            t += Time.deltaTime;
            float s = Mathf.Lerp(start, end, t / half);
            rt.localScale = new Vector3(s, s, 1f);
            yield return null;
        }
        
        CameraFlash.Instance.Flash();
        UIManager.Instance.cameraImages[backgroundIndex].SetActive(true);
        yield return new WaitForSeconds(zoomDuration);
        
        // Zoom Out (0.5s)
        t = 0f;
        while (t < half)
        {
            t += Time.deltaTime;
            float s = Mathf.Lerp(end, start, t / half);
            rt.localScale = new Vector3(s, s, 1f);
            yield return null;
        }

        // 정리
        rt.localScale = Vector3.one;
        SetPivotKeepTopLeft(rt, pivotOrigin);
        isAnimating = false;
    }

    // ----- Helpers -----

    private Camera GetUICamera()
    {
        if (!canvas) return null;
        return canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
    }

    // 스크린 포인트를 rt 내부의 pivot(0~1) 값으로 변환
    private static Vector2 ScreenPointToPivot01(RectTransform target, Vector2 screenPos, Camera uiCam)
    {
        RectTransformUtility.ScreenPointToLocalPointInRectangle(target, screenPos, uiCam, out Vector2 local);
        Rect r = target.rect;
        float px = Mathf.InverseLerp(r.xMin, r.xMax, local.x);
        float py = Mathf.InverseLerp(r.yMin, r.yMax, local.y);
        return new Vector2(Mathf.Clamp01(px), Mathf.Clamp01(py));
    }

    // 고정 앵커(Stretch 아님), 부모 좌상단 기준 가정:
    // pivot을 바꾸면서 화면 위치가 변하지 않도록 anchoredPosition 보정
    private static void SetPivotKeepTopLeft(RectTransform target, Vector2 newPivot)
    {
        Vector2 oldPivot = target.pivot;
        if (oldPivot == newPivot) return;

        Vector2 size = target.rect.size;
        Vector2 deltaPivot = newPivot - oldPivot;
        Vector2 deltaAnchored = new Vector2(deltaPivot.x * size.x, deltaPivot.y * size.y);

        target.pivot = newPivot;
        target.anchoredPosition += deltaAnchored;
    }
}
