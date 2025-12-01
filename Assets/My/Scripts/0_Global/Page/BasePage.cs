using System;
using System.Reflection;
using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

public abstract class BasePage<T> : MonoBehaviour where T : class
{
    [NonSerialized] protected T setting;

    public T Setting
    {
        get => setting;
        set => setting = value;
    }

    protected Settings jsonSetting;

    protected abstract string JsonPath { get; }
    protected abstract UniTask BuildContentAsync(CancellationToken token);

    protected GameObject mainCanvasObj;
    protected GameObject subCanvasObj;

    protected TextMeshProUGUI subtitleText1;
    protected TextMeshProUGUI subtitleText2;

    // 컨트롤러
    protected CameraController camController;
    protected InteractionController interactController;

    // 비디오 관련
    protected VideoPlayer videoPlayer;
    protected GameObject pageVideo;
    protected bool isPlayingVideo;
    protected int waitBeforePlayVideo;
    protected float outroFadeTime;

    protected CancellationTokenSource cancelToken;
    protected bool isCreated = false;

    // 기존 필드들 호환성 유지 (자식 클래스 사용 시)
    protected bool shouldTurnCamera
    {
        get => camController != null && camController.IsControlEnabled;
        set
        {
            if (camController) camController.IsControlEnabled = value;
        }
    }

    protected bool shouldRay
    {
        get => interactController != null && interactController.IsRayEnabled;
        set
        {
            if (interactController) interactController.IsRayEnabled = value;
        }
    }

    protected virtual void OnEnable()
    {
        cancelToken = new CancellationTokenSource();

        if (isCreated)
        {
            _ = FadeManager.Instance.FadeInAsync(JsonLoader.Instance.settings.fadeTime, false, cancelToken.Token);
        }
    }

    protected virtual void OnDisable()
    {
        if (cancelToken != null)
        {
            cancelToken.Cancel();
            cancelToken.Dispose();
            cancelToken = null;
        }

        if (pageVideo) pageVideo.SetActive(false);
    }

    protected virtual void Start()
    {
        jsonSetting ??= JsonLoader.Instance.settings;
        waitBeforePlayVideo = jsonSetting.waitBeforePlayVideo;
        outroFadeTime = jsonSetting.outroFadeTime;

        // 컴포넌트 찾기 또는 추가
        SetupControllers();

        StartAsync(cancelToken.Token).Forget();
    }

    private void SetupControllers()
    {
        // 메인 카메라에 컨트롤러가 없다면 붙여준다
        Camera mainCam = Camera.main;
        if (mainCam)
        {
            camController = UIUtility.GetOrAdd<CameraController>(mainCam.gameObject);
            interactController = UIUtility.GetOrAdd<InteractionController>(mainCam.gameObject);
            interactController.MainCamera = mainCam;

            // 이벤트 구독 (메모리 누수 방지를 위해 중복 제거 후 구독)
            interactController.OnTargetConfirmed -= HandleTargetConfirmed;
            interactController.OnTargetConfirmed += HandleTargetConfirmed;
        }
    }

    protected virtual void OnDestroy()
    {
        // 이벤트 구독 해제
        if (interactController)
        {
            interactController.OnTargetConfirmed -= HandleTargetConfirmed;
        }
    }
    
    // InteractionController에서 타겟 확정 시 호출되는 콜백
    protected virtual async UniTask HandleTargetConfirmed(BaseObject target)
    {
        // 0. 기본 유효성 검사
        if (!target) return;
        if (cancelToken == null) return; // 시작부터 취소된 상태면 리턴

        // 1. 카메라/레이 정지
        shouldTurnCamera = false;
        shouldRay = false;

        try
        {
            // 2. 줌 인
            SoundManager.Instance?.PlayZoom();
            if (camController && cancelToken != null)
                await camController.ZoomInAsync(cancelToken.Token);

            // 3. 대기
            if (cancelToken != null)
                await UniTask.Delay(waitBeforePlayVideo, DelayType.DeltaTime, PlayerLoopTiming.Update, cancelToken.Token);

            // 중간에 페이지가 꺼졌다면 중단
            if (cancelToken == null || !gameObject.activeInHierarchy) return;

            // 4. 오브젝트별 비디오 재생 (isPlayingVideo = true가 됨)
            target.OnRayConfirmed();

            // 5. 줌 아웃
            // cancelToken이 null인지 확인 (NRE 방지)
            if (camController && cancelToken != null)
            {
                await camController.ZoomOutAsync(cancelToken.Token);
            }

            // 6. 상태 복구
            // 비디오가 재생 중이라면 카메라를 켜지 않음 (GamePage에서 비디오가 끝날 때 켬)
            if (!isPlayingVideo)
            {
                shouldTurnCamera = true;
                shouldRay = true;
            }
        }
        catch (OperationCanceledException)
        {
            // 비동기 작업 중 취소(페이지 종료 등)되면 자연스럽게 종료
        }
        catch (Exception e)
        {
            Debug.LogError($"[BasePage] HandleTargetConfirmed Error: {e}");
        }
    }

    // GameManager 등 외부에서 호출할 수 있는 줌아웃 래퍼
    public async UniTask ZoomOutTarget(CancellationToken token)
    {
        if (camController) await camController.ZoomOutAsync(token);
    }

    protected virtual async UniTask StartAsync(CancellationToken token)
    {
        try
        {
            setting = JsonLoader.Instance.LoadJsonData<T>(JsonPath);
            if (setting == null)
            {
                Debug.LogError($"[{GetType().Name}] Settings not found at {JsonPath}");
                return;
            }

            await CreateUI(token);
            await FadeManager.Instance.FadeInAsync(JsonLoader.Instance.settings.fadeTime, external: token);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception e)
        {
            Debug.LogError($"[{GetType().Name}] Start failed: {e}");
        }
    }

    private async UniTask CreateUI(CancellationToken token)
    {
        mainCanvasObj = await UICreator.Instance.CreateCanvasAsync(token);
        mainCanvasObj.transform.SetParent(gameObject.transform);
        if (mainCanvasObj.TryGetComponent(out Canvas canvas1))
            canvas1.targetDisplay = jsonSetting.canvas1TargetMonitorIndex;

        subCanvasObj = await UICreator.Instance.CreateCanvasAsync(token);
        subCanvasObj.transform.SetParent(gameObject.transform);
        if (subCanvasObj.TryGetComponent(out Canvas canvas2) && subCanvasObj.TryGetComponent(out CanvasScaler canvasScaler))
        {
            canvas2.targetDisplay = jsonSetting.canvas2TargetMonitorIndex;
            canvasScaler.referenceResolution = new Vector2(2560, 720);
        }

        VideoSetting mainBG = GetFieldOrProperty<VideoSetting>(setting, "mainBackground");
        if (mainBG != null) await UICreator.Instance.CreateVideoPlayerAsync(mainBG, mainCanvasObj, token);

        VideoSetting subBG = GetFieldOrProperty<VideoSetting>(setting, "subBackground");
        if (subBG != null) await UICreator.Instance.CreateVideoPlayerAsync(subBG, subCanvasObj, token);

        await BuildContentAsync(token);
    }

    private static TField GetFieldOrProperty<TField>(object obj, string name) where TField : class
    {
        if (obj == null) return null;
        var type = obj.GetType();
        var fi = type.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        if (fi != null) return fi.GetValue(obj) as TField;
        var pi = type.GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        if (pi != null) return pi.GetValue(obj) as TField;
        return null;
    }
}