using System;
using System.Collections;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

public abstract class BasePage<T> : MonoBehaviour where T : class
{
    [NonSerialized] protected T setting; // 페이지별 설정 데이터
    protected Settings jsonSetting;

    protected abstract string JsonPath { get; }
    protected abstract Task BuildContentAsync();

    protected GameObject mainCanvasObj;
    protected GameObject subCanvasObj;

    protected Camera mainCamera;

    // 마우스 회전 설정
    private float mouseSensitivity;
    private float mouseSmoothing;
    private float minY;
    private float maxY;

    private float yaw;
    private float pitch;
    private Vector2 smoothedDelta;
    private Vector2 lastMousePos;
    private bool mouseInit;

    // 카메라 줌
    private float shutterThreshold;
    private readonly float moveThreshold = 0.1f; // "정지"로 볼 마우스 델타 임계값
    private float zoomInDuration;
    private float zoomOutDuration;
    private float flashWaitSeconds;
    private float zoomFOV;

    private float shutterTimer;
    private bool isZoomSequenceRunning;
    private float originFOV;

    protected bool isCaptured;
    protected bool isPlayingVideo;
    protected bool shouldTurnCamera;

    // 카메라 레이
    protected bool shouldRay;
    protected readonly float rayDistance = 100000f;
    private LayerMask hitMask;
    
    private GameObject hitTarget;
    private BaseObject currentTarget;

    protected VideoPlayer videoPlayer;
    protected GameObject pageVideo;

    private int gameSequence;

    protected virtual void OnEnable()
    {
        lastMousePos = Input.mousePosition;
        mouseInit = true;
        smoothedDelta = Vector2.zero;

        shutterTimer = 0f;
        isZoomSequenceRunning = false;
        isCaptured = false;
        
        pitch = 0;
        yaw = 0;
    }

    protected virtual void OnDisable()
    {
        if (pageVideo) pageVideo.SetActive(false);
    }

    protected virtual void Start()
    {
        jsonSetting ??= JsonLoader.Instance.settings;

        InitPage();
        _ = StartAsync();
    }


    protected virtual void LateUpdate()
    {
        if (!mainCamera || isPlayingVideo) return;

        if (shouldRay)
        {
            Ray ray = mainCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            if (Physics.Raycast(ray, out RaycastHit hit, rayDistance, hitMask))
            {
                GameObject newTarget = hit.collider.gameObject;

                if (newTarget.TryGetComponent(out BaseObject obj))
                {
                    // 같은 오브젝트 계속 맞춤
                    if (currentTarget == obj)
                    {
                        currentTarget.OnRayStay(Time.deltaTime);
                    }
                    else
                    {
                        // 타겟 변경됨 → 이전 타겟 리셋
                        currentTarget?.OnRayExit();

                        currentTarget = obj;
                        currentTarget.OnRayEnter();
                    }
                }
                else
                {
                    // BaseObject가 없는 오브젝트를 맞춘 경우
                    if (currentTarget)
                    {
                        currentTarget.OnRayExit();
                        currentTarget = null;
                    }
                }
            }
            else
            {
                // 레이가 아무 것도 안 맞음
                if (currentTarget)
                {
                    currentTarget.OnRayExit();
                    currentTarget = null;
                }
            }
        }

        if (shouldTurnCamera)
        {
            // 마우스 델타 계산
            Vector2 now = Input.mousePosition;
            Vector2 rawDelta = mouseInit ? Vector2.zero : (now - lastMousePos);
            lastMousePos = now;
            mouseInit = false;

            if (rawDelta.sqrMagnitude > (Screen.width * Screen.height))
                rawDelta = Vector2.zero;

            // 카메라 회전
            float lerpT = (mouseSmoothing <= 0f) ? 1f : Mathf.Clamp01(Time.unscaledDeltaTime * mouseSmoothing);
            smoothedDelta = Vector2.Lerp(smoothedDelta, rawDelta, lerpT);

            float dx = smoothedDelta.x * mouseSensitivity;
            float dy = smoothedDelta.y * mouseSensitivity * -1f;

            yaw += dx;
            pitch = Mathf.Clamp(pitch + dy, minY, maxY);
            mainCamera.transform.rotation = Quaternion.Euler(pitch, yaw, 0f);

            // 마우스 정지 감지
            bool isIdleThisFrame = rawDelta.magnitude <= moveThreshold;
            if (isIdleThisFrame)
            {
                shutterTimer += Time.deltaTime;

                // 설정한 시간 이상 정지 & 현재 줌 시퀀스가 돌고 있지 않다면 실행
                if (!isZoomSequenceRunning && shutterTimer >= shutterThreshold && !isCaptured)
                {
                    StartCoroutine(ZoomFlashSequence());
                    isCaptured = true;
                }
            }
            else
            {
                shutterTimer = 0f;
                // 움직임이 발생했는데 줌 시퀀스 중이면 즉시 원상복귀 시도
                if (isZoomSequenceRunning)
                {
                    StopAllCoroutines();
                    StartCoroutine(ZoomBackImmediately());
                }
            }
        }
    }

    protected virtual void InitPage()
    {
        jsonSetting ??= JsonLoader.Instance.settings;

        // Init Camera
        mainCamera = Camera.main;
        if (mainCamera)
        {
            Vector3 eul = mainCamera.transform.eulerAngles;
            yaw = eul.y;
            pitch = (eul.x > 180f) ? eul.x - 360f : eul.x;
            originFOV = mainCamera.fieldOfView;
        }

        mouseSensitivity = jsonSetting.mouseSensitivity;
        mouseSmoothing = jsonSetting.mouseSmoothing;
        minY = jsonSetting.minY;
        maxY = jsonSetting.maxY;

        shutterThreshold = jsonSetting.shutterThreshold;
        zoomInDuration = jsonSetting.zoomInDuration;
        zoomOutDuration = jsonSetting.zoomOutDuration;
        flashWaitSeconds = jsonSetting.flashWaitSeconds;
        zoomFOV = jsonSetting.zoomFOV;

        hitMask = LayerMask.GetMask("Object");
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
            await FadeManager.Instance.FadeInAsync(JsonLoader.Instance.settings.fadeTime);
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

        subCanvasObj.transform.position += new Vector3(3000, 0, 0);

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

    protected async Task HandleTitleButtonAsync()
    {
        await GameManager.Instance.ShowTitlePageOnly();
    }

    protected void HandlePlayPauseButton()
    {
        if (videoPlayer.isPlaying)
        {
            videoPlayer.Pause();
        }
        else
        {
            videoPlayer.Play();
        }
    }

    private IEnumerator ZoomFlashSequence()
    {
        isZoomSequenceRunning = true;

        // 1) 줌 인
        float start = mainCamera.fieldOfView;
        float end = zoomFOV;
        float t = 0f;

        while (t < 1f)
        {
            t += Time.deltaTime / Mathf.Max(0.0001f, zoomInDuration);
            mainCamera.fieldOfView = Mathf.Lerp(start, end, t);
            yield return null;
        }

        // 2) 플래시
        if (CameraFlash.Instance) CameraFlash.Instance.Flash();
        ActivateCameraImage(gameSequence);

        // 3) 대기
        yield return new WaitForSeconds(flashWaitSeconds);

        // 4) 줌 아웃(원복)
        start = mainCamera.fieldOfView;
        end = originFOV;
        t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / Mathf.Max(0.0001f, zoomOutDuration);
            mainCamera.fieldOfView = Mathf.Lerp(start, end, t);
            yield return null;
        }

        // 종료 정리
        isZoomSequenceRunning = false;
        shutterTimer = 0f;
    }

    protected virtual void ActivateCameraImage(int index)
    {
    }

    // 움직임이 감지되었을 때 진행 중 시퀀스를 끊고 즉시 원복
    private IEnumerator ZoomBackImmediately()
    {
        // 현재 상태를 원복으로 부드럽게 되돌린다
        float start = mainCamera.fieldOfView;
        float end = originFOV;
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / Mathf.Max(0.0001f, zoomOutDuration * 0.6f);
            mainCamera.fieldOfView = Mathf.Lerp(start, end, t);
            yield return null;
        }

        isZoomSequenceRunning = false;
        shutterTimer = 0f;
    }
    
    /// <summary>
    /// 비디오를 0초부터 다시 시작한다. (비활성 상태여도 자동으로 활성화 후 Prepare → Play)
    /// </summary>
    public void RestartVideoFromStart()
    {
        if (!isActiveAndEnabled) return;
        if (!videoPlayer)
        {
            Debug.LogWarning("[HubblePage] VideoPlayer reference is missing.");
            return;
        }

        if (pageVideo && !pageVideo.activeSelf)
            pageVideo.SetActive(true);

        StopAllCoroutines();
        StartCoroutine(RestartRoutine());
    }
    
    /// <summary>
    /// 비디오 재시작 코루틴. Stop → time/frame 0 → Prepare(필요시) → Play 순서.
    /// </summary>
    private IEnumerator RestartRoutine()
    {
        videoPlayer.Stop();
        videoPlayer.time = 0.0;
        if (videoPlayer.canSetTime) videoPlayer.frame = 0;

        if (!videoPlayer.isPrepared)
        {
            videoPlayer.Prepare();
            while (!videoPlayer.isPrepared)
                yield return null;
        }

        videoPlayer.Play();
        isPlayingVideo = true;
    }
}