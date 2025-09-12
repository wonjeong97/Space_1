using UnityEngine;

[RequireComponent(typeof(Renderer))]
public class ObjectAnimation : MonoBehaviour
{
    [Header("Sheet Layout")]
    [Tooltip("가로 프레임 수 (열)")]
    public int columns = 10;
    [Tooltip("세로 프레임 수 (행)")]
    public int rows = 10;
    [Tooltip("실제 사용되는 프레임 수 (0 ~ columns*rows)")]
    public int validFrames = 25;

    [Header("Playback")]
    [Tooltip("초당 프레임 수")]
    public float fps = 12f;
    [Tooltip("재생 시작 프레임 (0 ~ validFrames-1)")]
    public int startFrame = 0;
    [Tooltip("재생 모드: 정방향, 역방향, 핑퐁")]
    public PlayMode playMode = PlayMode.Forward;
    public enum PlayMode { Forward, Reverse, PingPong }

    [Header("V축 반전 보정")]
    [Tooltip("시트가 위->아래 인덱싱이 반대로 보일 때 체크")]
    public bool invertV = true;

    private static readonly int MainTexST = Shader.PropertyToID("_MainTex_ST");   // Built-in
    private static readonly int BaseMapST = Shader.PropertyToID("_BaseMap_ST");   // URP
    private Renderer rend;
    private MaterialPropertyBlock mpb;
    private int totalFrames;
    private float frameTime;
    private int direction = 1; // 핑퐁용
    private float elapsed;
    private int currentFrame;

    private void Awake()
    {
        rend = GetComponent<Renderer>();
        mpb = new MaterialPropertyBlock();

        // 유효 프레임 개수 제한
        int maxFrames = columns * rows;
        totalFrames = Mathf.Clamp(validFrames, 1, maxFrames);

        frameTime = 1f / Mathf.Max(1e-4f, fps);
        currentFrame = Mathf.Clamp(startFrame, 0, totalFrames - 1);

        // 초기 타일 설정
        ApplyTilingAndOffset(currentFrame);
    }

    private void Update()
    {
        elapsed += Time.unscaledDeltaTime; // 전역 시간 영향을 피하려면 unscaled 사용
        while (elapsed >= frameTime)
        {
            elapsed -= frameTime;
            StepFrame();
            ApplyTilingAndOffset(currentFrame);
        }
    }

    private void StepFrame()
    {
        switch (playMode)
        {
            case PlayMode.Forward:
                currentFrame = (currentFrame + 1) % totalFrames;
                break;
            case PlayMode.Reverse:
                currentFrame = (currentFrame - 1 + totalFrames) % totalFrames;
                break;
            case PlayMode.PingPong:
                currentFrame += direction;
                if (currentFrame >= totalFrames - 1 || currentFrame <= 0)
                    direction *= -1;
                break;
        }
    }

    private void ApplyTilingAndOffset(int frameIndex)
    {
        // 한 칸 크기
        float tileX = 1f / Mathf.Max(1, columns);
        float tileY = 1f / Mathf.Max(1, rows);

        // 인덱스 -> (u,v) 셀 좌표
        int u = frameIndex % columns;
        int v = frameIndex / columns;

        // 오프셋 계산 (텍스처 좌표계 상단 기준 보정)
        float offX = u * tileX;
        float offY = invertV ? (1f - (v + 1) * tileY) : v * tileY;

        // MPB로 _MainTex_ST / _BaseMap_ST에 동시 적용 (파이프라인 호환)
        // ST = (Tiling.x, Tiling.y, Offset.x, Offset.y)
        mpb.SetVector(MainTexST, new Vector4(tileX, tileY, offX, offY));
        mpb.SetVector(BaseMapST, new Vector4(tileX, tileY, offX, offY));
        rend.SetPropertyBlock(mpb);
    }

    /// <summary>외부에서 특정 프레임으로 점프</summary>
    public void SetFrame(int frame)
    {
        currentFrame = Mathf.Clamp(frame, 0, totalFrames - 1);
        elapsed = 0f;
        ApplyTilingAndOffset(currentFrame);
    }
}
