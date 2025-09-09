using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

[Serializable]
public class GameSetting
{
    public ImageSetting backgroundImage;

    public ButtonSetting titleButton;
    public ButtonSetting playPauseButton;
    public ButtonSetting skipButton;

    public ImageSetting crosshairImage;
    public ImageSetting inventoryImage;
    public ImageSetting[] contentsImages;

    public VideoSetting[] videos;
    public GameObjectSetting[] objects;

    public TextSetting[] missionTexts;
    public TextSetting[] missionSubTexts;

    public TextSetting playVideoText;
}

public enum StageEntry
{
    Hubble = 0,
    Moon = 1,
    Satellite = 2,
    Mars = 3,
    Rocket = 4,
    Final = 5,
}

/// <summary>
/// - 레이로 3초 조준(베이스에서 처리) → 확대 → OnRayConfirmed → 비디오 재생
/// - 단계별 오브젝트 1개만 활성화(Final은 오브젝트 없음)
/// - 비디오 재생 중: 일반 스테이지는 playVideoText, Final은 SubText6 출력 + 버튼/타이틀 숨김
/// - Final 종료 시: FadeManager로 메인 디스플레이 페이드아웃 → 5초 대기 → 타이틀 복귀(페이드인)
/// - 스킵/종료: 아이콘 그레이스케일 제거
/// </summary>
public class GamePage : BasePage<GameSetting>
{
    public static GamePage Instance { get; private set; }

    protected override string JsonPath => "JSON/HubbleSetting.json";

    // UI
    private GameObject titleButton;
    private GameObject playPauseButton;
    private GameObject skipButton;

    private readonly List<GameObject> missionTextGos = new();
    private readonly List<GameObject> missionSubTextGos = new();
    private GameObject playVideoTextGo;

    // 비디오 / 타겟 오브젝트
    private readonly List<GameObject> videoObjectList = new();
    private readonly List<GameObject> targetObjectsList = new();

    // 현재 단계
    private StageEntry currentStage = StageEntry.Hubble;

    // Final 관련
    private const int Sub6Index = 5; 
    private const int Sub7Index = 6; 
    [SerializeField] private float finalMainFadeDuration = 2.5f; // 메인 디스플레이 페이드아웃 시간

    private const float VideoFadeDuration = 0.1f;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else if (Instance != this) Destroy(gameObject);
    }

    protected override void OnEnable()
    {
        base.OnEnable();

        shouldTurnCamera = true;
        shouldRay = true;
        isPlayingVideo = false;

        ApplyStageActivation(currentStage);
        UpdateStageUI(currentStage);
        UpdateVideoUIVisible(false); // 비디오 미재생 시 버튼 숨김
    }

    protected override async Task BuildContentAsync()
    {
        // 1) 버튼
        (titleButton, _) = await UICreator.Instance.CreateSingleButtonAsync(setting.titleButton, subCanvasObj, CancellationToken.None);
        if (titleButton.TryGetComponent(out Button button1))
            button1.onClick.AddListener(() => _ = HandleTitleButtonAsync());

        (playPauseButton, _) = await UICreator.Instance.CreateSingleButtonAsync(setting.playPauseButton, subCanvasObj, CancellationToken.None);
        if (playPauseButton.TryGetComponent(out Button button2))
            button2.onClick.AddListener(HandlePlayPauseButton);
        playPauseButton.SetActive(false);

        (skipButton, _) = await UICreator.Instance.CreateSingleButtonAsync(setting.skipButton, subCanvasObj, CancellationToken.None);
        if (skipButton.TryGetComponent(out Button button3))
            button3.onClick.AddListener(HandleSkipButton);
        skipButton.SetActive(false);

        // 2) 크로스헤어
        GameObject crosshair = await UICreator.Instance.CreateSingleImageAsync(setting.crosshairImage, mainCanvasObj, CancellationToken.None);
        crosshair.AddComponent<Crosshair>();

        // 3) 인벤토리 + 콘텐츠 아이콘(초기 회색)
        GameObject inventory = await UICreator.Instance.CreateSingleImageAsync(setting.inventoryImage, mainCanvasObj, CancellationToken.None);
        foreach (var image in setting.contentsImages)
        {
            GameObject go = await UICreator.Instance.CreateSingleImageAsync(image, inventory, CancellationToken.None);
            if (go.TryGetComponent(out Image imageComponent))
                UICreator.Instance.LoadMaterialAndApply(imageComponent, "Materials/M_Grayscale.mat");
            UIManager.Instance.contentsImages.Add(go);
        }

        // 4) 미션/서브 텍스트 & 재생 안내 텍스트
        missionTextGos.Clear();
        if (setting.missionTexts != null)
        {
            foreach (var t in setting.missionTexts)
            {
                var go = await UICreator.Instance.CreateSingleTextAsync(t, mainCanvasObj, CancellationToken.None);
                go.SetActive(false);
                missionTextGos.Add(go);
            }
        }

        missionSubTextGos.Clear();
        if (setting.missionSubTexts != null)
        {
            foreach (var t in setting.missionSubTexts)
            {
                var go = await UICreator.Instance.CreateSingleTextAsync(t, subCanvasObj, CancellationToken.None);
                go.SetActive(false);
                missionSubTextGos.Add(go);
            }
        }

        playVideoTextGo = await UICreator.Instance.CreateSingleTextAsync(setting.playVideoText, subCanvasObj, CancellationToken.None);
        playVideoTextGo.SetActive(false);

        // 5) 비디오 / 타겟 오브젝트
        await CreateVideoObject();
        await CreateTargetObject();

        ApplyStageActivation(currentStage);
        UpdateStageUI(currentStage);
        UpdateVideoUIVisible(false);
    }

    private async Task HandleTitleButtonAsync()
    {
        await GameManager.Instance.ShowTitlePageOnly();
    }

    private async Task CreateVideoObject()
    {
        foreach (var videoSetting in setting.videos)
        {
            GameObject videoGo = await UICreator.Instance.CreateVideoPlayerAsync(videoSetting, mainCanvasObj, CancellationToken.None);
            videoGo.SetActive(false);

            // 종료 이벤트 바인딩
            if (videoGo.TryGetComponent(out VideoPlayer vp))
            {
                vp.loopPointReached -= OnVideoEnded;
                vp.loopPointReached += OnVideoEnded;
            }

            // (Final에서 비디오 자체를 페이드하지 않으므로, CanvasGroup은 필수 아님. 남겨도 무방)
            videoObjectList.Add(videoGo);
        }
    }

    private async Task CreateTargetObject()
    {
        // Final은 오브젝트 없음 → objects.Length는 5개(Hubble~Rocket)라고 가정
        for (int i = 0; i < setting.objects.Length; i++)
        {
            GameObject objectGo = await UICreator.Instance.CreateGameObjectAsync(setting.objects[i], mainCanvasObj, CancellationToken.None);

            // 레이어 Object 적용(레이캐스트 마스크는 BasePage에서 "Object" 사용)
            int layer = LayerMask.NameToLayer("Object");
            if (layer >= 0) objectGo.layer = layer;

            // 콜라이더 보장
            if (!objectGo.GetComponent<Collider>()) objectGo.AddComponent<BoxCollider>();

            // 단계별 컴포넌트 부착(같은 프리팹 재사용 가능)
            switch (i)
            {
                case (int)StageEntry.Hubble: objectGo.AddComponent<HubbleObject>(); break;
                case (int)StageEntry.Moon: objectGo.AddComponent<MoonObject>(); break;
                case (int)StageEntry.Satellite: objectGo.AddComponent<SatelliteObject>(); break;
                case (int)StageEntry.Mars: objectGo.AddComponent<MarsObject>(); break;
                case (int)StageEntry.Rocket: objectGo.AddComponent<RocketObject>(); break;
            }

            objectGo.SetActive(false);
            targetObjectsList.Add(objectGo);
        }
    }

    /// <summary>
    /// 인덱스로 특정 비디오 재생.
    /// - 다른 비디오는 Stop/비활성화
    /// - BasePage의 pageVideo/videoPlayer 갱신
    /// - 재생 중 UI 전환(ChangeSubDisplayOnVideo)
    /// </summary>
    public void PlayVideoByIndex(int index)
    {
        if (index < 0 || index >= videoObjectList.Count)
        {
            Debug.LogWarning($"[GamePage] Invalid video index {index}");
            return;
        }

        // 다른 비디오들은 정지 + 페이드아웃 후 비활성화
        for (int i = 0; i < videoObjectList.Count; i++)
        {
            if (i == index) continue;

            var go = videoObjectList[i];
            if (go.TryGetComponent(out VideoPlayer otherVp))
                otherVp.Stop();

            if (go.activeSelf)
                StartCoroutine(FadeOutAndDisableVideo(go, VideoFadeDuration));
            else
                go.SetActive(false);
        }

        GameObject selected = videoObjectList[index];

        // 선택 비디오 활성화 + 알파 0으로 준비
        SetAlpha(selected, 0f);
        if (!selected.activeSelf) selected.SetActive(true);

        if (selected.TryGetComponent(out VideoPlayer vp))
        {
            pageVideo = selected;
            videoPlayer = vp;
            videoPlayer.isLooping = false;

            // VideoPlayer 컴포넌트가 꺼져있으면 켠다 (Prepare 전에 필수)
            if (!videoPlayer.enabled) videoPlayer.enabled = true;

            // 재생 위치 초기화
            videoPlayer.time = 0;
            if (videoPlayer.canSetTime) videoPlayer.frame = 0;

            // 준비 → 완료 대기 → 재생 & 페이드인
            StartCoroutine(PreparePlayAndFadeIn(videoPlayer, selected));
        }
        else
        {
            // 혹시 VideoPlayer가 없다면 그냥 페이드인만
            StartCoroutine(FadeInVideo(selected, VideoFadeDuration));
            isPlayingVideo = true;
            ChangeSubDisplayOnVideo();
        }
    }

    private IEnumerator PreparePlayAndFadeIn(VideoPlayer vp, GameObject holder)
    {
        // 혹시 비활성 상태면 보장
        if (!holder.activeSelf) holder.SetActive(true);
        if (!vp.enabled) vp.enabled = true;

        // Prepare 호출 후 완료까지 대기
        vp.Prepare();
        while (!vp.isPrepared)
            yield return null;

        // 재생 시작
        vp.Play();
        isPlayingVideo = true;

        // 재생 중 UI 전환
        ChangeSubDisplayOnVideo();

        // 부드러운 페이드 인
        yield return StartCoroutine(FadeAlphaRoutine(holder, 0f, 1f, VideoFadeDuration));
    }

    // 영상 종료시
    private void OnVideoEnded(VideoPlayer vp)
    {
        if (currentStage == StageEntry.Final)
        {
            OnFinalVideoEnded();
            return;
        }

        isPlayingVideo = false;

        // 현재 비디오 오브젝트 페이드아웃 후 비활성화
        if (pageVideo)
        {
            if (videoPlayer) videoPlayer.Stop();
            StartCoroutine(FadeOutAndDisableVideo(pageVideo, VideoFadeDuration));
            pageVideo = null;
            videoPlayer = null;
        }

        // 완료 아이콘 컬러 복원(해당 비디오 인덱스)
        int videoIndex = videoObjectList.IndexOf(vp.gameObject);
        ClearGrayscaleIcon(videoIndex);

        if (currentStage == StageEntry.Rocket)
        {
            // 로켓 끝 → Final 시작
            StartFinalStage();
            return;
        }

        // 일반 단계 → 다음 스테이지
        NextStage();
        ApplyStageActivation(currentStage);
        UpdateStageUI(currentStage);
        UpdateVideoUIVisible(false);
    }

    private void ClearGrayscaleIcon(int videoIndex)
    {
        if (videoIndex >= 0 && videoIndex < UIManager.Instance.contentsImages.Count)
        {
            var iconGo = UIManager.Instance.contentsImages[videoIndex];
            if (iconGo && iconGo.TryGetComponent(out Image img))
                img.material = null;
        }
    }

    // 스킵 버튼
    private void HandleSkipButton()
    {
        if (!isPlayingVideo) return;

        // Final은 메인 디스플레이 페이드 로직 사용
        if (currentStage == StageEntry.Final)
        {
            OnFinalVideoEnded();
            return;
        }

        // 현재 재생 중 비디오 인덱스 (아이콘 컬러 복원용)
        int videoIndex = -1;
        if (pageVideo) videoIndex = videoObjectList.IndexOf(pageVideo);
        else if (videoPlayer) videoIndex = videoObjectList.IndexOf(videoPlayer.gameObject);

        // 정지 및 페이드아웃 후 비활성화
        if (videoPlayer) videoPlayer.Stop();
        if (pageVideo)
        {
            StartCoroutine(FadeOutAndDisableVideo(pageVideo, VideoFadeDuration));
            pageVideo = null;
        }

        videoPlayer = null;
        isPlayingVideo = false;

        // 아이콘 컬러 복원(스킵도 완료로 간주)
        ClearGrayscaleIcon(videoIndex);

        if (currentStage == StageEntry.Rocket)
        {
            // 로켓 스킵 → Final 즉시 시작
            StartFinalStage();
            return;
        }

        // 일반 단계 스킵 → 다음 스테이지
        UpdateVideoUIVisible(false);
        NextStage();
        ApplyStageActivation(currentStage);
        UpdateStageUI(currentStage);
    }

    // Final 시작: 오브젝트는 전부 숨기고, Final 비디오 즉시 재생 + Sub6 출력 + 버튼/타이틀 숨김
    private void StartFinalStage()
    {
        currentStage = StageEntry.Final;

        ApplyStageActivation(currentStage);

        // Final 재생 중 UI
        if (titleButton) titleButton.SetActive(false);
        UpdateVideoUIVisible(false);

        // 메인 미션 텍스트는 모두 숨김
        foreach (var missionText in missionTextGos)
            SafeSetActive(missionText, false);

        // 서브: Sub6만 표시
        ShowOnlySubIndex(Sub6Index);
        SafeSetActive(playVideoTextGo, false);

        // Final 비디오 재생(마지막 인덱스)
        PlayVideoByIndex(GetFinalVideoIndex());
    }

    // Final 종료 처리(메인 디스플레이 페이드아웃 + Sub7 + 5초 후 타이틀 복귀)
    private void OnFinalVideoEnded()
    {
        StartCoroutine(FadeOutMainAndShowSub7ThenTitle());
    }

    private IEnumerator FadeOutMainAndShowSub7ThenTitle()
    {
        // 버튼/타이틀 숨김 유지
        if (titleButton) titleButton.SetActive(false);
        UpdateVideoUIVisible(false);

        // 서브: Sub7 표시
        ShowOnlySubIndex(Sub7Index);
        SafeSetActive(playVideoTextGo, false);

        // 메인 디스플레이 페이드아웃 (FadeManager 사용)
        var fadeTask = FadeManager.Instance.FadeOutMainAsync(finalMainFadeDuration);
        while (!fadeTask.IsCompleted) yield return null;

        // 비디오/레퍼런스 정리
        if (videoPlayer) videoPlayer.Stop();
        if (pageVideo) pageVideo.SetActive(false);
        pageVideo = null;
        videoPlayer = null;
        isPlayingVideo = false;

        // 5초 대기
        yield return new WaitForSeconds(5f);

        // 타이틀 화면 복귀(내부에서 페이드인 수행)
        var titleTask = GameManager.Instance.ShowTitlePageOnly(false);
        while (!titleTask.IsCompleted) yield return null;
    }

    private int GetFinalVideoIndex()
    {
        // videos 배열의 마지막을 Final로 사용
        return Mathf.Max(0, setting.videos.Length - 1);
    }

    // 단계 전환(한 단계만 활성화 / Final은 오브젝트 없음)
    private void SetStage(StageEntry stage)
    {
        currentStage = stage;
        ApplyStageActivation(currentStage);
        UpdateStageUI(currentStage);
    }

    private void NextStage()
    {
        // Final은 다음 단계 없음 → 여기서는 Rocket까지 순환, Final은 명시적으로 진입
        int next = ((int)currentStage + 1) % targetObjectsList.Count; // targetObjectsList.Count == 5
        SetStage((StageEntry)next);
    }

    private void ApplyStageActivation(StageEntry stage)
    {
        if (stage == StageEntry.Final)
        {
            // Final: 모든 오브젝트 비활성
            foreach (var targetObject in targetObjectsList)
                if (targetObject)
                    targetObject.SetActive(false);

            return;
        }

        for (int i = 0; i < targetObjectsList.Count; i++)
        {
            bool active = (i == (int)stage);
            if (targetObjectsList[i] && targetObjectsList[i].activeSelf != active)
                targetObjectsList[i].SetActive(active);
        }
    }

    private void UpdateStageUI(StageEntry stage)
    {
        int idx = (int)stage;

        // 메인: 해당 스테이지 미션만 켜기 (Final엔 미션텍스트 없음 → 모두 숨김)
        for (int i = 0; i < missionTextGos.Count; i++)
            SafeSetActive(missionTextGos[i], stage != StageEntry.Final && i == idx);

        // 서브: 해당 스테이지 서브만 켜기 (Final은 StartFinalStage/ChangeSubDisplayOnVideo에서 제어)
        for (int i = 0; i < missionSubTextGos.Count; i++)
            SafeSetActive(missionSubTextGos[i], stage != StageEntry.Final && i == idx);

        // 영상 안내 텍스트 기본 숨김
        SafeSetActive(playVideoTextGo, false);

        // 비디오 미재생 시 버튼 숨김
        if (!isPlayingVideo) UpdateVideoUIVisible(false);

        // 타이틀 버튼은 기본 표시(단, Final 재생 중에는 숨김 처리)
        if (titleButton && stage != StageEntry.Final) titleButton.SetActive(true);
    }

    /// <summary>
    /// 비디오 재생 중 UI 전환:
    /// - 일반 스테이지: playVideoText만 표시 + 재생/스킵 버튼 보이기
    /// - Final: Sub6만 표시 + 타이틀/버튼 숨김
    /// </summary>
    private void ChangeSubDisplayOnVideo()
    {
        // 메인 미션 텍스트는 모두 숨김
        foreach (var missionText in missionTextGos)
            SafeSetActive(missionText, false);

        if (currentStage != StageEntry.Final)
        {
            // 모든 서브 텍스트 숨기고 playVideoText만 보이기
            foreach (var missionSubText in missionSubTextGos)
                SafeSetActive(missionSubText, false);

            SafeSetActive(playVideoTextGo, true);

            UpdateVideoUIVisible(true); // 재생/스킵 버튼 보이기
            if (titleButton) titleButton.SetActive(true);
        }
        else
        {
            // Final: Sub6 표시, 버튼/타이틀 숨김
            SafeSetActive(playVideoTextGo, false);
            ShowOnlySubIndex(Sub6Index);

            UpdateVideoUIVisible(false);
            if (titleButton) titleButton.SetActive(false);
        }
    }

    private static void SafeSetActive(GameObject go, bool active)
    {
        if (go && go.activeSelf != active) go.SetActive(active);
    }

    private void ShowOnlySubIndex(int subIndex)
    {
        for (int i = 0; i < missionSubTextGos.Count; i++)
            SafeSetActive(missionSubTextGos[i], i == subIndex);
    }

    private void UpdateVideoUIVisible(bool visible)
    {
        if (playPauseButton) playPauseButton.SetActive(visible);
        if (skipButton) skipButton.SetActive(visible);
    }

    public void ResetToFirstStage()
    {
        // 모든 비디오 정지/비활성
        if (videoObjectList != null)
        {
            foreach (var go in videoObjectList)
            {
                if (!go) continue;
                if (go.TryGetComponent(out VideoPlayer vp)) vp.Stop();
                go.SetActive(false);
            }
        }

        pageVideo = null;
        videoPlayer = null;
        isPlayingVideo = false;

        // 버튼 숨김
        if (playPauseButton) playPauseButton.SetActive(false);
        if (skipButton) skipButton.SetActive(false);

        // 아이콘 회색 재적용(처음 상태로)
        if (UIManager.Instance?.contentsImages != null)
        {
            foreach (var imgGo in UIManager.Instance.contentsImages)
            {
                if (!imgGo) continue;
                if (imgGo.TryGetComponent(out Image img))
                {
                    // 프로젝트에서 쓰는 초기 그레이스케일 머티리얼
                    UICreator.Instance.LoadMaterialAndApply(img, "Materials/M_Grayscale.mat");
                }
            }
        }

        // 스테이지/텍스트 초기화
        currentStage = StageEntry.Hubble;
        ApplyStageActivation(currentStage);
        UpdateStageUI(currentStage);
    }

    private void OnDrawGizmos()
    {
        if (!Camera.main) return;
        var cam = Application.isPlaying ? (mainCamera ? mainCamera : Camera.main) : Camera.main;

        Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        Gizmos.color = Color.red;
        Gizmos.DrawLine(ray.origin, ray.origin + ray.direction * rayDistance);
        Gizmos.DrawSphere(ray.origin + ray.direction * rayDistance, 0.1f);
    }

    private static bool TryGetRawOrCanvasGroup(GameObject go, out RawImage raw, out CanvasGroup cg)
    {
        raw = go ? go.GetComponent<RawImage>() : null;
        cg = go ? go.GetComponent<CanvasGroup>() : null;
        if (!raw && !cg && go)
        {
            // RawImage가 없다면 CanvasGroup으로라도 페이드할 수 있게 보장
            cg = go.AddComponent<CanvasGroup>();
        }

        return raw || cg;
    }

    private static void SetAlpha(GameObject go, float alpha)
    {
        if (!go) return;
        if (go.TryGetComponent(out RawImage raw))
        {
            var c = raw.color;
            raw.color = new Color(c.r, c.g, c.b, alpha);
        }
        else
        {
            var cg = go.GetComponent<CanvasGroup>() ?? go.AddComponent<CanvasGroup>();
            cg.alpha = alpha;
        }
    }

    private static IEnumerator FadeAlphaRoutine(GameObject go, float from, float to, float duration)
    {
        if (!go) yield break;
        TryGetRawOrCanvasGroup(go, out var raw, out var cg);

        float t = 0f;
        while (t < duration)
        {
            float p = duration <= 0f ? 1f : Mathf.Clamp01(t / duration);
            float a = Mathf.Lerp(from, to, p);

            if (raw)
            {
                var c = raw.color;
                raw.color = new Color(c.r, c.g, c.b, a);
            }
            else if (cg)
            {
                cg.alpha = a;
            }

            t += Time.deltaTime;
            yield return null;
        }

        // 마지막 값 보정
        if (raw)
        {
            var c = raw.color;
            raw.color = new Color(c.r, c.g, c.b, to);
        }
        else if (cg)
        {
            cg.alpha = to;
        }
    }

    private IEnumerator FadeInVideo(GameObject go, float duration)
    {
        if (!go) yield break;
        // 비활성 상태에서도 알파를 0으로 맞춰두고 활성화 → 페이드인
        SetAlpha(go, 0f);
        go.SetActive(true);
        yield return StartCoroutine(FadeAlphaRoutine(go, 0f, 1f, duration));
    }

    private IEnumerator FadeOutAndDisableVideo(GameObject go, float duration)
    {
        if (!go || !go.activeSelf) yield break;
        yield return StartCoroutine(FadeAlphaRoutine(go, 1f, 0f, duration));
        go.SetActive(false);
    }
}