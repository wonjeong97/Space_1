using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

[DisallowMultipleComponent]
public class PopupObject : MonoBehaviour
{
    private bool cleaned;

    private void OnDisable()
    {
        if (cleaned) return;
        cleaned = true;

        var creator = UICreator.Instance;

        // 자식들을 재귀적으로 모두 수집(자기 자신 제외), 깊이 우선 정리
        var all = GetComponentsInChildren<Transform>(true);
        var nodes = new List<(GameObject go, int depth)>(all.Length);
        for (int i = 0; i < all.Length; i++)
        {
            var tr = all[i];
            if (tr == transform) continue;
            nodes.Add((tr.gameObject, GetDepth(tr)));
        }
        // 깊은 것(자식)부터 먼저 해제/파괴
        nodes.Sort((a, b) => b.depth.CompareTo(a.depth));

        // 중복 파괴 방지용
        var destroyedRT = new HashSet<RenderTexture>();
        var destroyedTex2D = new HashSet<Texture2D>();
        var destroyedSprites = new HashSet<Sprite>();

        foreach (var (go, _) in nodes)
        {
            if (!go) continue;

            // 1) Addressables 인스턴스라면 해제하고 끝
            bool released = creator != null && creator.DestroyTrackedInstance(go);
            if (released) continue;

            // 2) 아니면 리소스 수동 정리 후 일반 Destroy

            // VideoPlayer RenderTexture 정리
            var vps = go.GetComponentsInChildren<VideoPlayer>(true);
            foreach (var vp in vps)
            {
                var rt = vp.targetTexture;
                if (rt != null && destroyedRT.Add(rt))
                {
                    rt.Release();
                    Object.Destroy(rt);
                }
                vp.targetTexture = null;
            }

            // RawImage 텍스처 정리 (RenderTexture는 위에서 처리)
            var raws = go.GetComponentsInChildren<RawImage>(true);
            foreach (var raw in raws)
            {
                if (raw.texture is RenderTexture)
                {
                    raw.texture = null;
                    continue;
                }
                if (raw.texture is Texture2D t2 && destroyedTex2D.Add(t2))
                {
                    Object.Destroy(t2);
                }
                raw.texture = null;
            }

            // Image의 동적 Sprite/Texture 정리 (Sprite.Create로 만든 경우)
            var imgs = go.GetComponentsInChildren<Image>(true);
            foreach (var img in imgs)
            {
                var spr = img.sprite;
                if (spr != null)
                {
                    var tex = spr.texture as Texture2D;
                    if (destroyedSprites.Add(spr))
                    {
                        Object.Destroy(spr);
                    }
                    if (tex != null && destroyedTex2D.Add(tex))
                    {
                        Object.Destroy(tex);
                    }
                }
                img.sprite = null;
            }

            Object.Destroy(go);
        }

        // 팝업 루트 파괴(팝업 루트는 Addressables 인스턴스가 아님)
        Object.Destroy(gameObject);
    }

    private static int GetDepth(Transform t)
    {
        int d = 0;
        var cur = t;
        while (cur != null)
        {
            d++;
            cur = cur.parent;
        }
        return d;
    }
}
