using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 크로스헤어가 마우스 커서를 따라다니게 하는 스크립트
/// </summary>
public class Crosshair : MonoBehaviour
{
    private RectTransform rectTransform;
    private Canvas canvas;

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        canvas = GetComponentInParent<Canvas>();
    }

    private void Update()
    {
        if (!rectTransform || !canvas) return;

        // 마우스 스크린 좌표 -> UI 좌표로 변환
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvas.transform as RectTransform,
            Input.mousePosition,
            canvas.worldCamera,
            out var localPoint
        );

        rectTransform.localPosition = localPoint;
    }
}