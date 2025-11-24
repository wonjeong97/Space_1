using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.UI;
using UnityEngine.Video;
using Object = UnityEngine.Object;

public class UICreator : MonoBehaviour
{
    public static UICreator Instance { get; private set; }

    private readonly List<GameObject> instances = new List<GameObject>();
    private readonly Dictionary<string, AsyncOperationHandle> assetCache = new Dictionary<string, AsyncOperationHandle>();

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else if (Instance != this) Destroy(gameObject);
    }

    private void OnDestroy()
    {
        DestroyAllTrackedInstances();
        ReleaseAllCachedAssets();
    }

    /// <summary>Addressables로 프리팹 비동기 인스턴스화</summary>
    private async UniTask<GameObject> InstantiateAsync(string key, Transform parent, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        AsyncOperationHandle<GameObject> handle = Addressables.InstantiateAsync(key, parent);
        try
        {
            // UIUtility.AwaitWithCancellation은 Task 반환이어도 UniTask 메서드에서 await 가능
            GameObject go = await UIUtility.AwaitWithCancellation(handle, token);
            if (go) instances.Add(go);
            return go;
        }
        catch (OperationCanceledException)
        {
            if (handle.IsValid() && handle.Status == AsyncOperationStatus.Succeeded && handle.Result)
                Addressables.ReleaseInstance(handle.Result);
            throw;
        }
        catch (Exception)
        {
            if (handle.IsValid() && handle.Result)
                Addressables.ReleaseInstance(handle.Result);
            return null;
        }
    }

    /// <summary>Addressables 에셋 로드를 캐시해 중복 로드 방지</summary>
    private async UniTask<T> LoadAssetWithCacheAsync<T>(string key, CancellationToken token) where T : Object
    {
        if (string.IsNullOrEmpty(key)) return null;

        if (assetCache.TryGetValue(key, out AsyncOperationHandle existing))
        {
            return existing.IsValid() ? (T)existing.Result : null;
        }

        token.ThrowIfCancellationRequested();

        AsyncOperationHandle<T> handle = Addressables.LoadAssetAsync<T>(key);
        T asset = await UIUtility.AwaitWithCancellation(handle, token); // Task 가능
        assetCache[key] = handle;

        return asset;
    }

    /// <summary>Addressables 에셋 캐시 전부 해제하고 비우기</summary>
    private void ReleaseAllCachedAssets()
    {
        foreach (KeyValuePair<string, AsyncOperationHandle> kv in assetCache)
        {
            if (kv.Value.IsValid()) Addressables.Release(kv.Value);
        }
        assetCache.Clear();
    }

    /// <summary>추적 중인 Addressables 인스턴스 전부 해제</summary>
    public void DestroyAllTrackedInstances()
    {
        for (int i = instances.Count - 1; i >= 0; --i)
        {
            GameObject go = instances[i];
            if (go != null) Addressables.ReleaseInstance(go);
        }
        instances.Clear();
    }

    /// <summary>추적 중인 특정 인스턴스 해제 시도 후 성공 여부 반환</summary>
    public bool DestroyTrackedInstance(GameObject go)
    {
        if (go == null) return false;

        int idx = instances.IndexOf(go);
        if (idx >= 0)
        {
            Addressables.ReleaseInstance(go);
            instances.RemoveAt(idx);
            return true;
        }
        return false;
    }

    // ---------- Font/Material helpers ----------

    /// <summary>폰트 키를 FontMap 기준으로 해석해 매핑된 키 반환</summary>
    private static string ResolveFontKey(string key)
    {
        Settings settings = JsonLoader.Instance?.settings;
        FontMaps fontMap = settings?.fontMap;
        if (fontMap == null || string.IsNullOrEmpty(key)) return key;

        FieldInfo field = typeof(FontMaps).GetField(key);
        if (field != null)
        {
            string mapped = field.GetValue(fontMap) as string;
            return string.IsNullOrEmpty(mapped) ? key : mapped;
        }
        return key;
    }

    /// <summary>폰트 키 매핑과 에셋 로드를 거쳐 TMP 텍스트 속성 적용</summary>
    private async UniTask ApplyFontAsync(TextMeshProUGUI uiText, string fontKey, string textValue,
        float fontSize, Color fontColor, TextAlignmentOptions alignment, CancellationToken token)
    {
        if (!uiText || string.IsNullOrEmpty(fontKey)) return;

        string mapped = ResolveFontKey(fontKey);
        TMP_FontAsset font = await LoadAssetWithCacheAsync<TMP_FontAsset>(mapped, token);
        if (!font) return;

        token.ThrowIfCancellationRequested();

        uiText.font = font;
        uiText.fontSize = fontSize;
        uiText.color = fontColor;
        uiText.alignment = alignment;
        uiText.text = textValue;
    }

    /// <summary>타깃 이미지에 Addressable로 로드한 머티리얼을 적용함 </summary>
    public void LoadMaterialAndApply(Image targetImage, string materialKey)
    {
        if (targetImage == null || string.IsNullOrEmpty(materialKey)) return;
        Addressables.LoadAssetAsync<Material>(materialKey).Completed += handle =>
        {
            if (handle.Status == AsyncOperationStatus.Succeeded)
            {
                targetImage.material = handle.Result;
            }
            else
            {
                Debug.LogWarning($"[UIManager] Material load failed: {materialKey}");
            }
        };
    }

    // ---------- Public creation APIs ----------

    /// <summary>캔버스 프리팹을 Addressables로 비동기 생성해 반환</summary>
    public async UniTask<GameObject> CreateCanvasAsync(CancellationToken token = default)
    {
        return await InstantiateAsync("Prefabs/CanvasPrefab.prefab", null, token);
    }

    public async UniTask<GameObject> CreateBackgroundImageAsync(ImageSetting setting, GameObject parent,
        CancellationToken token)
    {
        GameObject go = await CreateSingleImageAsync(setting, parent, token);
        if (go != null && go.TryGetComponent(out RectTransform rt))
        {
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = Vector2.zero;
            rt.localRotation = Quaternion.Euler(setting.rotation);
            rt.sizeDelta = setting.size;
        }
        return go;
    }

    /// <summary>여러 Text 항목을 비동기로 생성하고 모두 완료될 때까지 대기</summary>
    private async UniTask CreateTextsAsync(TextSetting[] settings, GameObject parent, CancellationToken token)
    {
        if (settings == null || settings.Length == 0) return;

        List<UniTask> tasks = new List<UniTask>(settings.Length);
        foreach (TextSetting s in settings)
            tasks.Add(CreateSingleTextAsync(s, parent, token).AsUniTask());

        await UniTask.WhenAll(tasks);
    }

    /// <summary>단일 Text 프리팹 생성 후 TMP 속성과 RectTransform 적용</summary>
    public async UniTask<GameObject> CreateSingleTextAsync(TextSetting setting, GameObject parent, CancellationToken token)
    {
        GameObject go = await InstantiateAsync("Prefabs/TextPrefab.prefab", parent.transform, token);
        if (go == null) return null;
        go.name = setting.name;

        if (go.TryGetComponent(out TextMeshProUGUI uiText))
        {
            await ApplyFontAsync(
                uiText,
                setting.fontName,
                setting.text,
                setting.fontSize,
                setting.fontColor,
                setting.alignment,
                token
            );
        }

        if (go.TryGetComponent(out RectTransform rt))
        {
            UIUtility.ApplyRect(
                rt,
                size: null,
                anchoredPos: new Vector2(setting.position.x, -setting.position.y),
                rotation: setting.rotation
            );
        }

        return go;
    }

    /// <summary>여러 Image 항목을 비동기로 생성하고 모두 완료될 때까지 대기</summary>
    private async UniTask CreateImagesAsync(ImageSetting[] images, GameObject parent, CancellationToken token)
    {
        if (images == null || images.Length == 0) return;

        List<UniTask> tasks = new List<UniTask>(images.Length);
        foreach (ImageSetting img in images)
            tasks.Add(CreateSingleImageAsync(img, parent, token).AsUniTask());

        await UniTask.WhenAll(tasks);
    }

    /// <summary>단일 Image 프리팹 생성 후 스프라이트/색/타입 및 RectTransform 적용</summary>
    public async UniTask<GameObject> CreateSingleImageAsync(ImageSetting setting, GameObject parent, CancellationToken token)
    {
        GameObject go = await InstantiateAsync("Prefabs/ImagePrefab.prefab", parent.transform, token);
        if (go == null) return null;
        go.name = setting.name;

        if (go.TryGetComponent(out Image image))
        {
            Texture2D texture = UIUtility.LoadTextureFromStreamingAssets(setting.sourceImage);
            if (texture != null)
            {
                image.sprite = Sprite.Create(
                    texture,
                    new Rect(0, 0, texture.width, texture.height),
                    new Vector2(0.5f, 0.5f)
                );
            }

            image.color = setting.color;
            image.type = (Image.Type)setting.type;
        }

        if (go.TryGetComponent(out RectTransform rt))
        {
            UIUtility.ApplyRect(
                rt,
                size: setting.size,
                anchoredPos: new Vector2(setting.position.x, -setting.position.y),
                rotation: setting.rotation,
                scale: setting.scale
            );
        }

        return go;
    }
    
    /// <summary> 튜토리얼 페이지에서 사용할 스타 프리팹 </summary>
    public async UniTask<GameObject> CreateSingleStarAsync(ImageSetting setting, GameObject parent, CancellationToken token)
    {
        GameObject go = await InstantiateAsync("Prefabs/StarPrefab.prefab", parent.transform, token);
        if (go == null) return null;
        go.name = setting.name;

        if (go.TryGetComponent(out RectTransform rt))
        {
            UIUtility.ApplyRect(
                rt,
                size: setting.size,
                anchoredPos: new Vector2(setting.position.x, -setting.position.y),
                rotation: setting.rotation
            );
        }

        return go;
    }
    
    /// <summary> 튜토리얼 페이지에서 사용할 크로스헤어 프리팹 </summary>
    public async UniTask<GameObject> CreateSingleCrosshairAsync(ImageSetting setting, GameObject parent, CancellationToken token)
    {
        GameObject go = await InstantiateAsync("Prefabs/CrosshairPrefab.prefab", parent.transform, token);
        if (go == null) return null;
        go.name = setting.name;

        if (go.TryGetComponent(out RectTransform rt))
        {
            UIUtility.ApplyRect(
                rt,
                size: setting.size,
                anchoredPos: new Vector2(setting.position.x, -setting.position.y),
                rotation: setting.rotation
            );
        }

        return go;
    }

    /// <summary>여러 Button 항목을 비동기로 생성하고 모두 완료될 때까지 대기</summary>
    private async UniTask<List<(GameObject button, GameObject addImage)>> CreateButtonsAsync(
        ButtonSetting[] settings, GameObject parent, CancellationToken token)
    {
        List<(GameObject button, GameObject addImage)> results = new List<(GameObject button, GameObject addImage)>();
        if (settings == null || settings.Length == 0) return results;

        List<UniTask<(GameObject button, GameObject addImage)>> tasks =
            new List<UniTask<(GameObject button, GameObject addImage)>>(settings.Length);

        foreach (ButtonSetting s in settings)
            tasks.Add(CreateSingleButtonAsync(s, parent, token));

        (GameObject button, GameObject addImage)[] created = await UniTask.WhenAll(tasks);
        results.AddRange(created);
        return results;
    }

    /// <summary>단일 Button 프리팹 생성 후 배경(비디오/이미지), 텍스트, 추가 이미지, RectTransform 적용 및 클릭 사운드 연결</summary>
     public async UniTask<(GameObject button, GameObject addImage)> CreateSingleButtonAsync(
        ButtonSetting setting, GameObject parent, CancellationToken token)
    {
        GameObject go = await InstantiateAsync("Prefabs/ButtonPrefab.prefab", parent.transform, token);
        if (go == null) return (null, null);
        go.name = setting.name;

        RectTransform rectTransform = go.GetComponent<RectTransform>();
        RawImage raw = go.GetComponent<RawImage>();
        VideoPlayer vp = go.GetComponent<VideoPlayer>();
        Button button = go.GetComponent<Button>();
        AudioSource audioSource = UIUtility.GetOrAdd<AudioSource>(go);

        if (rectTransform != null)
        {
            UIUtility.ApplyRect(
                rectTransform,
                size: setting.size,
                anchoredPos: new Vector2(setting.position.x, -setting.position.y),
                rotation: setting.rotation
            );
        }

        bool videoApplied = false;
        if (vp != null &&
            setting.buttonBackgroundVideo != null &&
            !string.IsNullOrEmpty(setting.buttonBackgroundVideo.fileName))
        {
            VideoManager.Instance.WireRawImageAndRenderTexture(
                vp,
                raw,
                new Vector2Int(Mathf.RoundToInt(setting.size.x), Mathf.RoundToInt(setting.size.y))
            );

            string url = VideoManager.Instance.ResolvePlayableUrl(setting.buttonBackgroundVideo.fileName);
            bool ok = await VideoManager.Instance.PrepareAndPlayAsync(
                vp, url, audioSource, setting.buttonBackgroundVideo.volume, token
            );
            videoApplied = ok;
        }

        if (!videoApplied && raw != null && setting.buttonBackgroundImage != null)
        {
            Texture2D tex = UIUtility.LoadTextureFromStreamingAssets(setting.buttonBackgroundImage.sourceImage);
            if (tex != null) raw.texture = tex;
            raw.color = setting.buttonBackgroundImage.color;
        }

        TextMeshProUGUI textComp = go.GetComponentInChildren<TextMeshProUGUI>(true);
        if (textComp != null && setting.buttonText != null && !string.IsNullOrEmpty(setting.buttonText.text))
        {
            await ApplyFontAsync(
                textComp,
                setting.buttonText.fontName,
                setting.buttonText.text,
                setting.buttonText.fontSize,
                setting.buttonText.fontColor,
                setting.buttonText.alignment,
                token
            );

            if (textComp.TryGetComponent(out RectTransform textRT))
            {
                UIUtility.ApplyRect(
                    textRT,
                    size: null,
                    anchoredPos: new Vector2(setting.buttonText.position.x, setting.buttonText.position.y),
                    rotation: setting.buttonText.rotation
                );
            }
        }

        GameObject addImgGo = null;
        if (setting.buttonAdditionalImage != null &&
            !string.IsNullOrEmpty(setting.buttonAdditionalImage.sourceImage))
        {
            addImgGo = await CreateSingleImageAsync(setting.buttonAdditionalImage, go, token);
            if (addImgGo != null && addImgGo.TryGetComponent(out RectTransform addRT))
            {
                UIUtility.ApplyRect(
                    addRT,
                    size: setting.buttonAdditionalImage.size,
                    anchoredPos: new Vector2(
                        setting.buttonAdditionalImage.position.x,
                        -setting.buttonAdditionalImage.position.y
                    ),
                    rotation: setting.buttonAdditionalImage.rotation
                );
            }
        }

        if (button != null)
        {
            string soundKey = setting.buttonSound;
            if (!string.IsNullOrEmpty(soundKey))
                button.onClick.AddListener(() => { SoundManager.Instance?.PlayByKey(soundKey); });
        }

        return (go, addImgGo);
    }

    /// <summary>VideoPlayer 프리팹 생성 후 RenderTexture/오디오 연결 및 재생 준비</summary>
    public async UniTask<GameObject> CreateVideoPlayerAsync(VideoSetting setting, GameObject parent, CancellationToken token, bool shouldMask = false)
    {
        if (setting == null || string.IsNullOrEmpty(setting.fileName) || VideoManager.Instance == null)
            return null;

        token.ThrowIfCancellationRequested();

        GameObject go;
        if (shouldMask)
        {
            go = await InstantiateAsync("Prefabs/VideoPlayerPrefab.prefab", parent.transform, token);
        }
        else
        {
            go = await InstantiateAsync("Prefabs/VideoPlayerPrefab_NoMask.prefab", parent.transform, token);
        }

        if (go == null) return null;
        go.name = setting.name;

        VideoPlayer vp = go.GetComponent<VideoPlayer>();
        RawImage raw = go.GetComponentInChildren<RawImage>();
        AudioSource audioSource = UIUtility.GetOrAdd<AudioSource>(go);

        if (vp == null)
        {
            Debug.LogError("[UICreator] Video prefab missing VideoPlayer component");
            return go;
        }

        if (go.TryGetComponent(out RectTransform rt))
        {
            UIUtility.ApplyRect(
                rt,
                size: setting.size,
                anchoredPos: new Vector2(setting.position.x, -setting.position.y),
                rotation: Vector3.zero
            );
        }

        VideoManager.Instance.WireRawImageAndRenderTexture(
            vp,
            raw,
            new Vector2Int(Mathf.RoundToInt(setting.size.x), Mathf.RoundToInt(setting.size.y))
        );

        string url = VideoManager.Instance.ResolvePlayableUrl(setting.fileName);
        bool ok = await VideoManager.Instance.PrepareAndPlayAsync(vp, url, audioSource, setting.volume, token);

        if (!ok)
            Debug.LogError($"[UICreator] Failed to prepare video: {url}");

        return go;
    }
    
    /// <summary>페이지 루트를 생성하고 RectTransform 설정 후 하위 요소들(텍스트/이미지/버튼) 병렬 생성</summary>
    public async UniTask<GameObject> CreatePageAsync(PageSetting page, GameObject parent, CancellationToken token)
    {
        GameObject pageRoot = new GameObject(string.IsNullOrEmpty(page.name) ? "GeneratedPage" : page.name);
        pageRoot.transform.SetParent(parent.transform, false);

        RectTransform rt = pageRoot.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(page.position.x, -page.position.y);
        rt.sizeDelta = page.size;

        List<UniTask> jobs = new List<UniTask>(4)
        {
            CreateTextsAsync(page.texts, pageRoot, token),
            CreateImagesAsync(page.images, pageRoot, token),
            CreateButtonsAsync(page.buttons, pageRoot, token).AsUniTask()
        };

        await UniTask.WhenAll(jobs);
        return pageRoot;
    }

    public async UniTask<GameObject> CreateEffectAsync(EffectSetting setting, GameObject parent, CancellationToken token = default)
    {
        GameObject go = await InstantiateAsync("Prefabs/EffectPrefab.prefab", parent.transform, token);
        if (go == null) return null;
        go.name = setting.name;

        if (go.TryGetComponent(out RectTransform rt) && (parent.TryGetComponent(out RectTransform parentRect)))
        {
            UIUtility.ApplyRect(
                rt,
                size: setting.size,
                anchoredPos: new Vector2(setting.position.x, -setting.position.y)
            );
        }

        return go;
    }
    public async UniTask<GameObject> CreateGameObjectAsync(GameObjectSetting setting, GameObject parent, CancellationToken token = default)
    {
        GameObject go = await InstantiateAsync("Prefabs/GameObjectPrefab.prefab", parent.transform, token);
        if (go == null) return null;
        go.name = setting.name;

        if (go.TryGetComponent(out Transform trans))
        {
            trans.parent = parent.transform;
            trans.position = setting.position;
            trans.localScale = setting.size;
            trans.rotation = Quaternion.Euler(setting.rotation);
        }

        return go;
    }
}