using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

public abstract class BasePage<T> : MonoBehaviour where T : class
{
    [NonSerialized] protected T setting; // 페이지별 설정 데이터
    protected Settings jsonSetting;

    protected abstract string JsonPath { get; }
    protected abstract Task BuildContentAsync();

    protected GameObject mainCanvasObj;
    protected GameObject subCanvasObj;

    protected virtual void Start()
    {
        if (jsonSetting == null)
        {
            jsonSetting = JsonLoader.Instance.settings;
        }

        _ = StartAsync();
    }

    protected virtual async Task StartAsync()
    {
        try
        {
            setting = JsonLoader.Instance.LoadJsonData<T>(JsonPath);
            if (setting == null)
            {
                Debug.LogError($"[{GetType().Name}] Settings not found at {JsonPath}");
                return;
            }

            await CreateUI();
            await FadeManager.Instance.FadeInAsync(JsonLoader.Instance.settings.fadeTime, true);
        }
        catch (OperationCanceledException)
        {
            Debug.LogWarning($"[{GetType().Name}] Start canceled.");
        }
        catch (Exception e)
        {
            Debug.LogError($"[{GetType().Name}] Start failed: {e}");
        }
    }

    private async Task CreateUI()
    {
        mainCanvasObj = await UICreator.Instance.CreateCanvasAsync(CancellationToken.None);
        mainCanvasObj.transform.SetParent(gameObject.transform);

        subCanvasObj = await UICreator.Instance.CreateCanvasAsync(CancellationToken.None);
        subCanvasObj.transform.SetParent(gameObject.transform);
        if (subCanvasObj.TryGetComponent(out Canvas canvas) &&
            subCanvasObj.TryGetComponent(out CanvasScaler canvasScaler))
        {
            canvas.targetDisplay = 1;
            canvasScaler.referenceResolution = new Vector2(1920, 540);
        }

        var mainBackground = GetFieldOrProperty<VideoSetting>(setting, "mainBackground");
        if (mainBackground != null)
            await UICreator.Instance.CreateVideoPlayerAsync(mainBackground, mainCanvasObj, CancellationToken.None);

        var subBackground = GetFieldOrProperty<VideoSetting>(setting, "subBackground");
        if (subBackground != null)
            await UICreator.Instance.CreateVideoPlayerAsync(subBackground, subCanvasObj, CancellationToken.None);

        await BuildContentAsync();
    }

    /// <summary>
    /// 지정한 이름의 필드나 프로퍼티 값을 가져오는 유틸 메서드
    /// (JSON 세팅에서 필드/프로퍼티 구분 없이 접근 가능)
    /// </summary>
    private static TField GetFieldOrProperty<TField>(object obj, string name) where TField : class
    {
        if (obj == null) return null;

        var type = obj.GetType();

        // Field 먼저
        var fi = type.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        if (fi != null)
            return fi.GetValue(obj) as TField;

        // Property 다음
        var pi = type.GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        if (pi != null)
            return pi.GetValue(obj) as TField;

        return null;
    }
}