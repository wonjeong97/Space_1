using System;
using UnityEngine;
using UnityEngine.Video;

[DisallowMultipleComponent]
public class HubbleEffect : MonoBehaviour
{
    private GamePage gamePage;

    private RectTransform crosshairRT;
    private RectTransform selfRT;
    private bool wasOverlapping;

    //private float overlapTimer;
    //private float requiredOverlapTime = 2f;

    private void Awake()
    {
        if (gamePage == null)
            gamePage = GetComponentInParent<GamePage>();

        selfRT = GetComponent<RectTransform>();

        // 씬에서 Crosshair 컴포넌트를 찾아 자동 할당
        if (!crosshairRT)
        {
            var crosshair = FindObjectOfType<Crosshair>();
            if (crosshair) crosshairRT = crosshair.GetComponent<RectTransform>();
        }
    }

    private void Update()
    {
        /*if (!selfRT || !crosshairRT) return;

        bool overlapping = IsUIOverlapping(selfRT, crosshairRT);

        if (overlapping)
        {
            overlapTimer += Time.deltaTime;

            // Enter 조건: 겹친 상태가 requiredOverlapTime 이상 지속됐을 때
            if (!wasOverlapping && overlapTimer >= requiredOverlapTime)
            {
                wasOverlapping = true;

                HubblePage.Instance.HubbleVideo.SetActive(true);
                HubblePage.Instance.RestartVideoFromStart();

                // 이펙트 비활성화
                gameObject.SetActive(false);
            }
        }
        else
        {
            // 겹침 해제 → 타이머 초기화
            overlapTimer = 0f;
            wasOverlapping = false;
        }*/
    }

    private static bool IsUIOverlapping(RectTransform a, RectTransform b)
    {
        Vector3[] aCorners = new Vector3[4];
        Vector3[] bCorners = new Vector3[4];
        a.GetWorldCorners(aCorners);
        b.GetWorldCorners(bCorners);

        // AABB(min/max) 계산
        MinMax2D aMM = GetMinMax(aCorners);
        MinMax2D bMM = GetMinMax(bCorners);

        bool xOverlap = aMM.max.x >= bMM.min.x && bMM.max.x >= aMM.min.x;
        bool yOverlap = aMM.max.y >= bMM.min.y && bMM.max.y >= aMM.min.y;
        return xOverlap && yOverlap;
    }

    private struct MinMax2D
    {
        public Vector2 min, max;
    }

    private static MinMax2D GetMinMax(Vector3[] corners)
    {
        float minX = corners[0].x, maxX = corners[0].x;
        float minY = corners[0].y, maxY = corners[0].y;
        for (int i = 1; i < 4; i++)
        {
            Vector3 c = corners[i];
            if (c.x < minX) minX = c.x;
            if (c.x > maxX) maxX = c.x;
            if (c.y < minY) minY = c.y;
            if (c.y > maxY) maxY = c.y;
        }

        return new MinMax2D { min = new Vector2(minX, minY), max = new Vector2(maxX, maxY) };
    }
}