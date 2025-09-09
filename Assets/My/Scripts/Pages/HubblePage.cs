using System;
using System.Collections;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

[Serializable]
public class HubbleSetting
{
    public ImageSetting backgroundImage;
    public ButtonSetting titleButton;
    public ImageSetting crosshairImage;
    public ImageSetting inventoryImage;
    public ImageSetting[] contentsImages;
    public ImageSetting[] cameraImages;
    public VideoSetting hubbleVideo;
    public GameObjectSetting targetObject;
    public TextSetting missionText;
    public TextSetting subText;
}

public class HubblePage : BasePage<HubbleSetting>
{
    public static HubblePage Instance { get; private set; }

    protected override string JsonPath => "JSON/Game1Setting.json";
    private CancellationTokenSource cursorCts;

    // Video
    private VideoPlayer videoPlayer;
    public GameObject HubbleVideo { get; private set; }

    // Camera
    private Camera mainCamera;

    // ===== 마우스 기반 카메라 회전 설정 =====
    [Header("Camera Mouse Look")] [SerializeField]
    private float sensitivity = 0.12f; // px당 회전(도)

    [SerializeField] private float smoothing = 10f; // 0이면 즉시, 값이 클수록 부드럽게
    [SerializeField] private float minPitch = -60f; // 위/아래 회전 제한
    [SerializeField] private float maxPitch = 60f;
    [SerializeField] private bool invertY = false;

    private float yaw;
    private float pitch;
    private Vector2 smoothedDelta; // 보간된 Δmouse
    private Vector2 lastMousePos;
    private bool mouseInit;

    [Header("Auto Capture (Idle Zoom + Flash)")] [SerializeField]
    private float idleThresholdSeconds = 3f; // 마우스 정지 감지 시간

    [SerializeField] private float moveThreshold = 0.1f; // "정지"로 볼 마우스 델타 임계값
    [SerializeField] private float zoomDuration = 0.5f; // 줌 인 시간
    [SerializeField] private float zoomBackDuration = 0.5f; // 원복 시간
    [SerializeField] private float flashWaitSeconds = 1f; // 플래시 후 대기 시간
    [SerializeField] private float targetFov = 35f; // 퍼스펙티브 카메라용 목표 FOV
    [SerializeField] private float targetOrthoSize = 2.8f; // 오쏘 카메라용 목표 사이즈

    private float idleTimer;
    private bool isZoomSequenceRunning;
    private float baseFov;
    private float baseOrthoSize;
    
    [Header("Debug Center Ray")]
    [SerializeField] private bool showCenterRay = true;     // 레이 표시 On/Off
    [SerializeField] private float debugRayLength = 500f;   // 레이 길이
    [SerializeField] private Color debugRayColor = Color.cyan; // 레이 색상
    [SerializeField] private bool drawHitPointCross = true; // 충돌 지점 표시

    private bool isCaptured;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else if (Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        // 메인 카메라 탐색
        mainCamera = Camera.main;
        if (!mainCamera)
        {
            mainCamera = FindObjectOfType<Camera>();
        }

        if (mainCamera)
        {
            var eul = mainCamera.transform.eulerAngles;
            // 유니티의 Euler는 0~360 범위라 음수 보정
            yaw = eul.y;
            pitch = (eul.x > 180f) ? eul.x - 360f : eul.x;

            if (mainCamera.orthographic) baseOrthoSize = mainCamera.orthographicSize;
            else baseFov = mainCamera.fieldOfView;
        }
        else
        {
            Debug.LogWarning("[HubblePage] Main Camera not found.");
        }
    }

    private void OnEnable()
    {
        // 첫 프레임 델타 스파이크 방지
        lastMousePos = Input.mousePosition;
        mouseInit = true;
        smoothedDelta = Vector2.zero;

        idleTimer = 0f;
        isZoomSequenceRunning = false;
        isCaptured = false;
    }

    private void Update()
    {
        if (!mainCamera) return;

        // ===== 마우스 델타 계산 (기존) =====
        Vector2 now = Input.mousePosition;
        Vector2 rawDelta = mouseInit ? Vector2.zero : (now - lastMousePos);
        lastMousePos = now;
        mouseInit = false;

        if (rawDelta.sqrMagnitude > (Screen.width * Screen.height))
            rawDelta = Vector2.zero;

        // ===== 1) 카메라 회전 (기존) =====
        float lerpT = (smoothing <= 0f) ? 1f : Mathf.Clamp01(Time.unscaledDeltaTime * smoothing);
        smoothedDelta = Vector2.Lerp(smoothedDelta, rawDelta, lerpT);

        float dx = smoothedDelta.x * sensitivity;
        float dy = smoothedDelta.y * sensitivity * (invertY ? 1f : -1f);

        yaw += dx;
        pitch = Mathf.Clamp(pitch + dy, minPitch, maxPitch);
        mainCamera.transform.rotation = Quaternion.Euler(pitch, yaw, 0f);

        // ===== 2) 마우스 정지 감지 =====
        bool isIdleThisFrame = rawDelta.magnitude <= moveThreshold;
        if (isIdleThisFrame)
        {
            idleTimer += Time.deltaTime;

            // 3초 이상 정지 & 현재 줌 시퀀스가 돌고 있지 않다면 실행
            if (!isZoomSequenceRunning && idleTimer >= idleThresholdSeconds && !isCaptured)
            {
                StartCoroutine(ZoomFlashSequence());
                isCaptured = true;
            }
        }
        else
        {
            idleTimer = 0f;
            // 움직임이 발생했는데 줌 시퀀스 중이면 즉시 원상복귀 시도
            // (끊기지 않길 원하면 이 블록을 제거)
            if (isZoomSequenceRunning)
            {
                StopAllCoroutines();
                StartCoroutine(ZoomBackImmediately());
            }
        }
        
        if (showCenterRay)
        {
            // 화면 중앙에서 월드로 향하는 레이 (퍼스펙티브/오쏘 모두 대응)
            Ray ray = mainCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));

            // 레이 그리기
            Debug.DrawRay(ray.origin, ray.direction * debugRayLength, debugRayColor);

            // (선택) 실제 충돌 지점 시각화
            if (drawHitPointCross && Physics.Raycast(ray, out RaycastHit hit, debugRayLength))
            {
                // 히트 지점에 작은 십자 표시
                float s = 0.15f;
                Vector3 p = hit.point;

                Debug.DrawLine(p - Vector3.right * s, p + Vector3.right * s, debugRayColor);
                Debug.DrawLine(p - Vector3.up * s,    p + Vector3.up * s,    debugRayColor);
                Debug.DrawLine(p - Vector3.forward * s, p + Vector3.forward * s, debugRayColor);
            }
        }
    }

    private IEnumerator ZoomFlashSequence()
    {
        isZoomSequenceRunning = true;

        // 1) 줌 인
        if (mainCamera.orthographic)
        {
            float start = mainCamera.orthographicSize;
            float end = targetOrthoSize;
            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / Mathf.Max(0.0001f, zoomDuration);
                mainCamera.orthographicSize = Mathf.Lerp(start, end, t);
                yield return null;
            }
        }
        else
        {
            float start = mainCamera.fieldOfView;
            float end = targetFov;
            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / Mathf.Max(0.0001f, zoomDuration);
                mainCamera.fieldOfView = Mathf.Lerp(start, end, t);
                yield return null;
            }
        }

        // 2) 플래시
        if (CameraFlash.Instance != null)
            CameraFlash.Instance.Flash();

        // 3) 1초 대기
        yield return new WaitForSeconds(flashWaitSeconds);

        // 4) 줌 아웃(원복)
        if (mainCamera.orthographic)
        {
            float start = mainCamera.orthographicSize;
            float end = baseOrthoSize;
            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / Mathf.Max(0.0001f, zoomBackDuration);
                mainCamera.orthographicSize = Mathf.Lerp(start, end, t);
                yield return null;
            }
        }
        else
        {
            float start = mainCamera.fieldOfView;
            float end = baseFov;
            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / Mathf.Max(0.0001f, zoomBackDuration);
                mainCamera.fieldOfView = Mathf.Lerp(start, end, t);
                yield return null;
            }
        }

        // 종료 정리
        isZoomSequenceRunning = false;
        idleTimer = 0f;
    }

    // 움직임이 감지되었을 때 진행 중 시퀀스를 끊고 즉시 원복
    private IEnumerator ZoomBackImmediately()
    {
        // 현재 상태를 원복으로 부드럽게 되돌린다
        if (mainCamera.orthographic)
        {
            float start = mainCamera.orthographicSize;
            float end = baseOrthoSize;
            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / Mathf.Max(0.0001f, zoomBackDuration * 0.6f);
                mainCamera.orthographicSize = Mathf.Lerp(start, end, t);
                yield return null;
            }
        }
        else
        {
            float start = mainCamera.fieldOfView;
            float end = baseFov;
            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / Mathf.Max(0.0001f, zoomBackDuration * 0.6f);
                mainCamera.fieldOfView = Mathf.Lerp(start, end, t);
                yield return null;
            }
        }

        isZoomSequenceRunning = false;
        idleTimer = 0f;
    }

    protected override async Task BuildContentAsync()
    {
        (GameObject titleButton, _) = await UICreator.Instance.CreateSingleButtonAsync(setting.titleButton, subCanvasObj, CancellationToken.None);
        if (titleButton.TryGetComponent(out Button button))
        {
            button.onClick.AddListener(() => _ = HandleTitleButtonAsync());
        }

        GameObject crosshair = await UICreator.Instance.CreateSingleImageAsync(setting.crosshairImage, mainCanvasObj, CancellationToken.None);
        // 필요 시 크로스헤어 컴포넌트/레이캐스트 비활성화 재활성

        GameObject inventory = await UICreator.Instance.CreateSingleImageAsync(setting.inventoryImage, mainCanvasObj, CancellationToken.None);
        for (int i = 0; i < setting.contentsImages.Length; i++)
        {
            var image = setting.contentsImages[i];
            GameObject go = await UICreator.Instance.CreateSingleImageAsync(image, inventory, CancellationToken.None);

            if (go.TryGetComponent(out Image imageComponent))
            {
                UICreator.Instance.LoadMaterialAndApply(imageComponent, "Materials/M_Grayscale.mat");
            }

            UIManager.Instance.contentsImages.Add(go);
        }

        foreach (var cameraImage in setting.cameraImages)
        {
            GameObject go = await UICreator.Instance.CreateSingleImageAsync(cameraImage, inventory, CancellationToken.None);
            UIManager.Instance.cameraImages.Add(go);
            go.SetActive(false);
        }

        await UICreator.Instance.CreateSingleTextAsync(setting.missionText, mainCanvasObj, CancellationToken.None);

        HubbleVideo = await UICreator.Instance.CreateVideoPlayerAsync(setting.hubbleVideo, mainCanvasObj, CancellationToken.None);
        if (HubbleVideo.TryGetComponent(out VideoPlayer vp))
        {
            videoPlayer = vp;
            HubbleVideo.SetActive(false);
        }

        await UICreator.Instance.CreateSingleTextAsync(setting.subText, subCanvasObj, CancellationToken.None);
        await UICreator.Instance.CreateGameObjectAsync(setting.targetObject, gameObject, CancellationToken.None);
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

        if (HubbleVideo && !HubbleVideo.activeSelf)
            HubbleVideo.SetActive(true);

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
    }
}