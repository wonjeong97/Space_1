using UnityEngine;
using Cysharp.Threading.Tasks;
using System.Threading;

public class CameraController : MonoBehaviour
{
    [Header("Settings")]
    public float mouseSensitivity = 2.0f;
    public float mouseSmoothing = 10.0f;
    public float upLimit = -40f; 
    public float downLimit = 40f;
    public float leftLimit = -135f;
    public float rightLimit = 135f;

    [Header("Zoom Settings")]
    public float zoomFOV = 20f;
    public float zoomInDuration = 1.0f;
    public float zoomOutDuration = 1.0f;

    // 내부 상태 변수
    private float yaw;
    private float pitch;
    private Vector2 smoothedDelta;
    private float originFOV;
    private Camera cam;

    // 서보모터 연동 변수
    private float _servoSyncTimer = 0f;
    private float _accumulatedServoDelta = 0f;
    private const float SERVO_SYNC_INTERVAL = 0.1f;

    public bool IsControlEnabled { get; set; } = false; // 제어 활성화 여부

    private void Awake()
    {
        cam = GetComponent<Camera>();
        originFOV = cam.fieldOfView;

        Vector3 angles = transform.eulerAngles;
        yaw = angles.y;
        pitch = (angles.x > 180f) ? angles.x - 360f : angles.x;
    }

    private void Start()
    {
        // JSON 설정 적용 (JsonLoader 의존성)
        var settings = JsonLoader.Instance?.settings;
        if (settings != null)
        {
            mouseSensitivity = settings.mouseSensitivity;
            mouseSmoothing = settings.mouseSmoothing;
            upLimit = -Mathf.Abs(settings.Up); 
            downLimit = Mathf.Abs(settings.Down);
            zoomInDuration = settings.zoomInDuration;
            zoomOutDuration = settings.zoomOutDuration;
            zoomFOV = settings.zoomFOV;
        }
    }

    private void LateUpdate()
    {
        if (!IsControlEnabled) return;

        HandleRotation();
    }

    private void HandleRotation()
    {
        float rawX = Input.GetAxisRaw("Mouse X");
        float rawY = Input.GetAxisRaw("Mouse Y");

        // 부드러운 회전 처리
        float lerpT = (mouseSmoothing <= 0f) ? 1f : Mathf.Clamp01(Time.unscaledDeltaTime * mouseSmoothing);
        smoothedDelta = Vector2.Lerp(smoothedDelta, new Vector2(rawX, rawY), lerpT);

        float dx = smoothedDelta.x * mouseSensitivity;
        float dy = smoothedDelta.y * mouseSensitivity * -1f;

        yaw += dx;
        yaw = Mathf.Clamp(yaw, leftLimit, rightLimit);
        pitch = Mathf.Clamp(pitch + dy, upLimit, downLimit);

        transform.rotation = Quaternion.Euler(pitch, yaw, 0f);

        // 서보모터 통신 로직
        ProcessServoCommand(dx);
    }
    
    public void ResetRotation()
    {
        yaw = 0f;
        pitch = -45f;
        smoothedDelta = Vector2.zero;
        transform.rotation = Quaternion.Euler(-45f, 0f, 0f);
    }

    private void ProcessServoCommand(float dx)
    {
        _accumulatedServoDelta += dx;
        _servoSyncTimer += Time.deltaTime;

        if (_servoSyncTimer >= SERVO_SYNC_INTERVAL)
        {
            int deltaDeg = Mathf.RoundToInt(_accumulatedServoDelta);

            if (Mathf.Abs(deltaDeg) >= 1)
            {
                string direction = deltaDeg > 0 ? "left" : "right";
                int absValue = Mathf.Abs(deltaDeg);
                string cmd = $"{direction} {absValue} {SERVO_SYNC_INTERVAL}";
                
                ArduinoManager.Instance?.ExcuteCommand(cmd);
                _accumulatedServoDelta = 0f;
            }
            _servoSyncTimer = 0f;
        }
    }

    // 줌 인/아웃 기능 (UniTask 활용)
    public async UniTask ZoomInAsync(CancellationToken token)
    {
        await TweenFOV(originFOV, zoomFOV, zoomInDuration, token);
    }

    public async UniTask ZoomOutAsync(CancellationToken token)
    {
        await TweenFOV(cam.fieldOfView, originFOV, zoomOutDuration, token);
    }

    private async UniTask TweenFOV(float start, float end, float duration, CancellationToken token)
    {
        float elapsed = 0f;
        while (elapsed < 1f)
        {
            token.ThrowIfCancellationRequested();
            elapsed += Time.deltaTime / Mathf.Max(0.0001f, duration);
            cam.fieldOfView = Mathf.Lerp(start, end, elapsed);
            await UniTask.Yield(PlayerLoopTiming.Update, token);
        }
        cam.fieldOfView = end;
    }
}