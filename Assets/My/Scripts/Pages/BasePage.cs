using System;
using System.Collections;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

/// <summary>
/// 공통 페이지 베이스
/// - 카메라 중앙 레이캐스트로 BaseObject 타겟 추적
/// - 같은 타겟을 dwellThreshold 초 이상 조준하면 확대 후 OnRayConfirmed() 호출
/// - 확대/축소 시간, 확대 FOV는 jsonSetting에서 로드
/// </summary>
public abstract class BasePage<T> : MonoBehaviour where T : class
{
    [NonSerialized] protected T setting;        // 페이지별 설정 데이터
    protected Settings jsonSetting;

    protected abstract string JsonPath { get; }
    protected abstract Task BuildContentAsync();

    protected GameObject mainCanvasObj;
    protected GameObject subCanvasObj;

    protected Camera mainCamera;

    // ===== 마우스 기반 카메라 회전 설정 =====
    private float mouseSensitivity;
    private float mouseSmoothing;
    private float minY;
    private float maxY;

    protected float yaw;
    protected float pitch;
    private Vector2 smoothedDelta;
    private Vector2 lastMousePos;
    private bool mouseInit;

    // ===== 카메라 줌 파라미터 (idle 기반 트리거는 제거됨) =====
    private float zoomInDuration;
    private float zoomOutDuration;
    private float zoomFOV;
    private bool isZoomSequenceRunning;
    private float originFOV;

    protected bool isPlayingVideo;
    protected bool shouldTurnCamera;

    // ===== 카메라 중앙 레이 =====
    protected bool shouldRay;
    protected readonly float rayDistance = 100000f;
    private LayerMask hitMask;

    private BaseObject currentTarget;

    // ===== 조준 유지(dwell) 로직 =====
    [SerializeField] private float dwellThreshold = 3f; // n초 이상 조준 시 확정
    private float dwellTimer;
    private bool dwellInProgress; // 확대 중복 방지

    // ===== 비디오 참조(파생 페이지에서 갱신/사용) =====
    protected VideoPlayer videoPlayer;
    protected GameObject pageVideo;

    protected virtual void OnEnable()
    {
        lastMousePos = Input.mousePosition;
        mouseInit = true;
        smoothedDelta = Vector2.zero;

        isZoomSequenceRunning = false;

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

        // ===== 레이캐스트로 타겟 추적 및 dwell 누적 =====
        if (shouldRay)
        {
            Ray ray = mainCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            if (Physics.Raycast(ray, out RaycastHit hit, rayDistance, hitMask))
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
                            StartCoroutine(ZoomThenConfirmCurrentTarget());
                        }
                    }
                    else
                    {
                        // 타겟 교체
                        ResetDwell();
                        currentTarget?.OnRayExit();
                        currentTarget = obj;
                        currentTarget.OnRayEnter();
                    }
                }
                else
                {
                    // BaseObject가 아닌 것에 맞음 → 리셋
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
                // 아무것도 맞추지 못함 → 리셋
                if (currentTarget)
                {
                    currentTarget.OnRayExit();
                    ResetDwell();
                    currentTarget = null;
                }
            }
        }

        // ===== 카메라 회전 (마우스 정지 트리거는 완전히 제거됨) =====
        if (shouldTurnCamera)
        {
            Vector2 now = Input.mousePosition;
            Vector2 rawDelta = mouseInit ? Vector2.zero : (now - lastMousePos);
            lastMousePos = now;
            mouseInit = false;

            // 드문 예외 입력 보호
            if (rawDelta.sqrMagnitude > (Screen.width * Screen.height))
                rawDelta = Vector2.zero;

            float lerpT = (mouseSmoothing <= 0f) ? 1f : Mathf.Clamp01(Time.unscaledDeltaTime * mouseSmoothing);
            smoothedDelta = Vector2.Lerp(smoothedDelta, rawDelta, lerpT);

            float dx = smoothedDelta.x * mouseSensitivity;
            float dy = smoothedDelta.y * mouseSensitivity * -1f;

            yaw += dx;
            pitch = Mathf.Clamp(pitch + dy, minY, maxY);
            mainCamera.transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
        }
    }

    private void ResetDwell()
    {
        dwellTimer = 0f;
        dwellInProgress = false;
    }

    /// <summary>
    /// dwell 확정 → 줌인 → (옵션) 플래시 → OnRayConfirmed() → 줌아웃
    /// 비디오는 OnRayConfirmed()에서(=오브젝트/파생 페이지에서) 실행.
    /// </summary>
    private IEnumerator ZoomThenConfirmCurrentTarget()
    {
        if (!mainCamera || currentTarget == null)
        {
            dwellInProgress = false;
            yield break;
        }

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

        // 2) (옵션) 플래시
        //if (CameraFlash.Instance) CameraFlash.Instance.Flash();

        // 3) 조준 확정: 오브젝트가 비디오 실행(예: GamePage.PlayVideoByIndex)
        currentTarget.OnRayConfirmed();

        // 4) 줌 아웃(즉시 원복. 비디오 종료 후 원복하고 싶으면 이 위치를 이동)
        start = mainCamera.fieldOfView;
        end = originFOV;
        t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / Mathf.Max(0.0001f, zoomOutDuration);
            mainCamera.fieldOfView = Mathf.Lerp(start, end, t);
            yield return null;
        }

        isZoomSequenceRunning = false;
        ResetDwell(); // 다음 조준을 위해 초기화
    }

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
        mouseSmoothing  = jsonSetting.mouseSmoothing;
        minY            = jsonSetting.minY;
        maxY            = jsonSetting.maxY;

        // 줌 파라미터
        zoomInDuration  = jsonSetting.zoomInDuration;
        zoomOutDuration = jsonSetting.zoomOutDuration;
        zoomFOV         = jsonSetting.zoomFOV;

        // 레이 마스크(Object 레이어)
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

        // 두 캔버스가 겹치지 않도록 화면 밖으로 이동(멀티 디스플레이 구성)
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
    /// 지정한 이름의 필드나 프로퍼티 값을 가져오는 유틸 (JSON 세팅에서 공통 접근)
    /// </summary>
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
    
    protected void HandlePlayPauseButton()
    {
        if (!videoPlayer) return;
        if (videoPlayer.isPlaying) videoPlayer.Pause();
        else videoPlayer.Play();
    }

    /// <summary>
    /// (공용) 현재 pageVideo/videoPlayer 기준으로 0초부터 다시 시작
    /// </summary>
    public void RestartVideoFromStart()
    {
        if (!isActiveAndEnabled) return;
        if (!videoPlayer)
        {
            Debug.LogWarning($"[{GetType().Name}] VideoPlayer reference is missing.");
            return;
        }

        if (pageVideo && !pageVideo.activeSelf)
            pageVideo.SetActive(true);

        StopAllCoroutines();
        StartCoroutine(RestartRoutine());
    }

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
