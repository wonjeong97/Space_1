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

    // ===== 마우스 기반 카메라 회전 설정 =====
    private Camera mainCamera;

    private float mouseSensitivity;
    private float mouseSmoothing;
    private float Up;
    private float Down;

    private float yaw;
    private float pitch;
    private Vector2 smoothedDelta;
    private Vector2 lastMousePos;
    private bool mouseInit;

    // ===== 카메라 줌 파라미터 (idle 기반 트리거는 제거됨) =====
    private float zoomInDuration;
    private float zoomOutDuration;
    private float zoomFOV;
    private float originFOV;

    protected bool isPlayingVideo;
    protected bool shouldTurnCamera;

    // ===== 카메라 중앙 레이 =====
    protected bool shouldRay;
    private const float RayDistance = 100000f;
    private LayerMask hitMask;

    private BaseObject currentTarget;

    // ===== 조준 유지(dwell) 로직 =====
    private float dwellThreshold; // n초 이상 조준 시 확정
    private float dwellTimer;
    private bool dwellInProgress; // 확대 중복 방지

    // ===== 비디오 참조(파생 페이지에서 갱신/사용) =====
    protected VideoPlayer videoPlayer;
    protected GameObject pageVideo;

    protected float outroFadeTime;
    private float waitBeforePlayVideo;
    
    #region Unity Life-cycle

    protected virtual void OnEnable()
    {
        lastMousePos = Input.mousePosition;
        mouseInit = true;
        smoothedDelta = Vector2.zero;

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
        // 카메라가 없거나 비디오 재생 중에는 마우스 이동 금지
        if (!mainCamera || isPlayingVideo) return;

        // 레이캐스트로 타겟 추적 및 dwell 누적
        if (shouldRay)
        {
            Ray ray = mainCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            if (Physics.Raycast(ray, out RaycastHit hit, RayDistance, hitMask))
            {
                GameObject newGo = hit.collider.gameObject;

                if (newGo.TryGetComponent(out BaseObject obj))
                {
                    if (currentTarget == obj)
                    {
                        // 동일 타겟 계속 조준
                        currentTarget.OnRayStay(Time.deltaTime);
                        dwellTimer += Time.deltaTime;

                        // dwell 충족 & 아직 확대 시퀀스 시작 안 했을 때
                        if (!dwellInProgress && dwellTimer >= dwellThreshold)
                        {
                            dwellInProgress = true;
                            shouldTurnCamera = false;
                            StartCoroutine(ZoomTargetObject());
                        }
                    }
                    else
                    {
                        ResetDwell();
                        currentTarget?.OnRayExit();
                        currentTarget = obj;
                        currentTarget.OnRayEnter();
                    }
                }
                else
                {
                    // BaseObject가 아닌 것에 맞음 -> 리셋
                    if (currentTarget)
                    {
                        currentTarget.OnRayExit();
                        ResetDwell();
                        currentTarget = null;
                    }
                }
            }
            else
            {
                // 아무것도 맞추지 못함 -> 리셋
                if (currentTarget)
                {
                    currentTarget.OnRayExit();
                    ResetDwell();
                    currentTarget = null;
                }
            }
        }

        // 카메라 회전
        if (shouldTurnCamera)
        {
            Vector2 now = Input.mousePosition;
            Vector2 rawDelta = mouseInit ? Vector2.zero : (now - lastMousePos);
            lastMousePos = now;
            mouseInit = false;

            // 예외 입력 보호
            if (rawDelta.sqrMagnitude > (Screen.width * Screen.height))
                rawDelta = Vector2.zero;

            float lerpT = (mouseSmoothing <= 0f) ? 1f : Mathf.Clamp01(Time.unscaledDeltaTime * mouseSmoothing);
            smoothedDelta = Vector2.Lerp(smoothedDelta, rawDelta, lerpT);

            float dx = smoothedDelta.x * mouseSensitivity;
            float dy = smoothedDelta.y * mouseSensitivity * -1f;

            yaw += dx;
            pitch = Mathf.Clamp(pitch + dy, Up, Down);
            mainCamera.transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
        }
    }

    #endregion

    /// <summary> Dwell 초기화 </summary>
    private void ResetDwell()
    {
        dwellTimer = 0f;
        dwellInProgress = false;
    }

    /// <summary> 타겟 줌 인 -> 비디오 플레이 -> 줌 아웃 </summary>
    private IEnumerator ZoomTargetObject()
    {
        if (!mainCamera || !currentTarget)
        {
            dwellInProgress = false;
            yield break;
        }

        // 줌 인
        float start = mainCamera.fieldOfView;
        float end = zoomFOV;
        float time = 0f;
        while (time < 1f)
        {
            time += Time.deltaTime / Mathf.Max(0.0001f, zoomInDuration);
            mainCamera.fieldOfView = Mathf.Lerp(start, end, time);
            yield return null;
        }

        yield return new WaitForSeconds(waitBeforePlayVideo);
        // 오브젝트에 맞는 비디오 실행
        currentTarget.OnRayConfirmed();

        // 줌 아웃
        start = mainCamera.fieldOfView;
        end = originFOV;
        time = 0f;
        while (time < 1f)
        {
            time += Time.deltaTime / Mathf.Max(0.0001f, zoomOutDuration);
            mainCamera.fieldOfView = Mathf.Lerp(start, end, time);
            yield return null;
        }

        shouldTurnCamera = true;
        ResetDwell(); // 다음 조준을 위해 초기화
    }

    /// <summary> setting 제이슨 파일에서 초기값을 불러옴 </summary>
    protected virtual void InitPage()
    {
        jsonSetting ??= JsonLoader.Instance.settings;

        // 카메라 초기화
        mainCamera = Camera.main;
        if (mainCamera)
        {
            Vector3 eul = mainCamera.transform.eulerAngles;
            yaw = eul.y;
            pitch = (eul.x > 180f) ? eul.x - 360f : eul.x;
            originFOV = mainCamera.fieldOfView;
        }

        // 카메라 회전 파라미터
        mouseSensitivity = jsonSetting.mouseSensitivity;
        mouseSmoothing = jsonSetting.mouseSmoothing;
        Up = -jsonSetting.Up; 
        Down = -jsonSetting.Down; // json 세팅에서 편의를 위해 Up, Down에 -를 곱함

        // 줌 파라미터
        dwellThreshold = jsonSetting.dwellThreshold;
        zoomInDuration = jsonSetting.zoomInDuration;
        zoomOutDuration = jsonSetting.zoomOutDuration;
        zoomFOV = jsonSetting.zoomFOV;

        // 레이 마스크(Object 레이어)
        hitMask = LayerMask.GetMask("Object");

        outroFadeTime = jsonSetting.outroFadeTime;
        waitBeforePlayVideo = jsonSetting.waitBeforePlayVideo;
    }

    /// <summary> 시작 메서드, UI 생성 후 페이드인으로 페이지 시작 </summary>
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

    /// <summary> 페이지 UI 생성 메서드 </summary>
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

        VideoSetting mainBackground = GetFieldOrProperty<VideoSetting>(setting, "mainBackground");
        if (mainBackground != null)
            await UICreator.Instance.CreateVideoPlayerAsync(mainBackground, mainCanvasObj, CancellationToken.None);

        VideoSetting subBackground = GetFieldOrProperty<VideoSetting>(setting, "subBackground");
        if (subBackground != null)
            await UICreator.Instance.CreateVideoPlayerAsync(subBackground, subCanvasObj, CancellationToken.None);

        await BuildContentAsync();
    }

    /// <summary> 지정한 이름의 필드나 프로퍼티 값을 가져오는 유틸 (JSON 세팅에서 공통 접근) </summary>
    private static TField GetFieldOrProperty<TField>(object obj, string name) where TField : class
    {
        if (obj == null) return null;

        var type = obj.GetType();

        // Field
        var fi = type.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        if (fi != null)
            return fi.GetValue(obj) as TField;

        // Property
        var pi = type.GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        if (pi != null)
            return pi.GetValue(obj) as TField;

        return null;
    }
}