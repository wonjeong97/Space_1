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
    private const float FinalMainFadeDuration = 2.5f; // 메인 디스플레이 페이드아웃 시간

    private const float VideoFadeDuration = 0.1f;

    #region Unity Life-cycle

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

    #endregion

    protected override async Task BuildContentAsync()
    {
        // === 서브 디스플레이 버튼 생성 ===
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

        // === 크로스헤어 생성 ===
        GameObject crosshair = await UICreator.Instance.CreateSingleImageAsync(setting.crosshairImage, mainCanvasObj, CancellationToken.None);
        crosshair.AddComponent<Crosshair>();

        // === 인벤토리 및 허블, 달, 인공위성, 화성, 로켓 이미지 생성 ===
        GameObject inventory = await UICreator.Instance.CreateSingleImageAsync(setting.inventoryImage, mainCanvasObj, CancellationToken.None);
        foreach (ImageSetting image in setting.contentsImages)
        {
            GameObject go = await UICreator.Instance.CreateSingleImageAsync(image, inventory, CancellationToken.None);
            if (go.TryGetComponent(out Image imageComponent))
            {
                // 생성한 이미지에 회색 셰이더 적용
                UICreator.Instance.LoadMaterialAndApply(imageComponent, "Materials/M_Grayscale.mat");
            }

            UIManager.Instance.contentsImages.Add(go);
        }

        // === 메인,서브 텍스트 및 비디오 재생 텍스트 생성 ===
        // 메인 텍스트
        missionTextGos.Clear();
        if (setting.missionTexts != null)
        {
            foreach (TextSetting t in setting.missionTexts)
            {
                GameObject go = await UICreator.Instance.CreateSingleTextAsync(t, mainCanvasObj, CancellationToken.None);
                go.SetActive(false);
                missionTextGos.Add(go);
            }
        }

        // 서브 텍스트
        missionSubTextGos.Clear();
        if (setting.missionSubTexts != null)
        {
            foreach (TextSetting t in setting.missionSubTexts)
            {
                GameObject go = await UICreator.Instance.CreateSingleTextAsync(t, subCanvasObj, CancellationToken.None);
                go.SetActive(false);
                missionSubTextGos.Add(go);
            }
        }

        // 비디오 텍스트
        playVideoTextGo = await UICreator.Instance.CreateSingleTextAsync(setting.playVideoText, subCanvasObj, CancellationToken.None);
        playVideoTextGo.SetActive(false);

        // === 스테이지 별 비디오 및 타겟 오브젝트 생성 ===
        await CreateVideoObject();
        await CreateTargetObject();

        ApplyStageActivation(currentStage);
        UpdateStageUI(currentStage);
        UpdateVideoUIVisible(false);
    }

    #region Sub-Display Button Click Event

    private void HandlePlayPauseButton()
    {
        if (!videoPlayer) return;
        if (videoPlayer.isPlaying) videoPlayer.Pause();
        else videoPlayer.Play();
    }

    private async Task HandleTitleButtonAsync()
    {
        await GameManager.Instance.ShowTitlePageOnly();
    }

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
            StartCoroutine(FadeOutVideo(pageVideo, VideoFadeDuration));
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

    #endregion

    #region Create

    /// <summary> 각 스테이지 별 비디오 플레이어 생성 </summary>
    private async Task CreateVideoObject()
    {
        foreach (VideoSetting videoSetting in setting.videos)
        {
            GameObject videoGo = await UICreator.Instance.CreateVideoPlayerAsync(videoSetting, mainCanvasObj, CancellationToken.None);
            videoGo.SetActive(false);

            // 종료 이벤트 바인딩
            if (videoGo.TryGetComponent(out VideoPlayer vp))
            {
                vp.loopPointReached -= OnVideoEnded;
                vp.loopPointReached += OnVideoEnded;
            }

            videoObjectList.Add(videoGo);
        }
    }

    /// <summary> 각 스테이지 별 타겟 오브젝트 생성 </summary>>
    private async Task CreateTargetObject()
    {
        for (int i = 0; i < setting.objects.Length; i++)
        {
            GameObject objectGo = await UICreator.Instance.CreateGameObjectAsync(setting.objects[i], mainCanvasObj, CancellationToken.None);
            switch (i) // 단계별 컴포넌트 부착
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

    #endregion

    #region Video Method

    /// <summary> 인덱스로 특정 비디오 재생 </summary>
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

            GameObject go = videoObjectList[i];
            if (go.TryGetComponent(out VideoPlayer otherVp))
                otherVp.Stop();

            if (go.activeSelf)
                StartCoroutine(FadeOutVideo(go, VideoFadeDuration));
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

            if (!videoPlayer.enabled) videoPlayer.enabled = true;

            videoPlayer.time = 0; // 처음부터 재생
            if (videoPlayer.canSetTime) videoPlayer.frame = 0;

            // 준비 -> 완료 대기 -> 재생 및 페이드인
            StartCoroutine(PlayVideoAndFadeIn(videoPlayer, selected));
        }
        else
        {
            StartCoroutine(FadeInVideo(selected, VideoFadeDuration));
            isPlayingVideo = true;
            ChangeSubDisplayOnVideo();
        }
    }

    /// <summary> 비디오를 페이드인 하고 재생함 </summary>
    private IEnumerator PlayVideoAndFadeIn(VideoPlayer vp, GameObject go)
    {
        if (!go.activeSelf) go.SetActive(true);
        if (!vp.enabled) vp.enabled = true;

        // 준비 완료까지 대기
        vp.Prepare();
        while (!vp.isPrepared)
            yield return null;

        vp.Play();
        isPlayingVideo = true;

        ChangeSubDisplayOnVideo(); // 서브 디스플레이 변경
        yield return StartCoroutine(RawImageFade(go, 0f, 1f, VideoFadeDuration));
    }

    /// <summary> 비디오 종료 시 실행 함수 </summary>
    private void OnVideoEnded(VideoPlayer vp)
    {
        if (currentStage == StageEntry.Final)
        {
            OnFinalVideoEnded();
            return;
        }

        isPlayingVideo = false;

        // 현재 비디오 오브젝트 페이드아웃 및 비활성화
        if (pageVideo)
        {
            if (videoPlayer) videoPlayer.Stop();
            StartCoroutine(FadeOutVideo(pageVideo, VideoFadeDuration));
            pageVideo = null;
            videoPlayer = null;
        }

        // 스테이지 아이콘 색 복원
        int videoIndex = videoObjectList.IndexOf(vp.gameObject);
        ClearGrayscaleIcon(videoIndex);

        if (currentStage == StageEntry.Rocket)
        {
            StartFinalStage();
            return;
        }

        NextStage(); // 다음 스테이지로 갱신
        ApplyStageActivation(currentStage); // 스테이지 별 타깃 오브젝트 갱신
        UpdateStageUI(currentStage); // 미션, 서브 텍스트 업데이트
        UpdateVideoUIVisible(false); // 재생/밈춤, 건너뛰기 버튼 숨김
    }

    /// <summary> 모든 체험이 끝난 후 처음으로 되돌아감 </summary>
    private void OnFinalVideoEnded()
    {
        // 메인 디스플레이 페이드아웃 + Sub7 + 5초 후 타이틀 복귀
        StartCoroutine(Outro());
    }

    #endregion

    /// <summary> 마지막 스테이지를 시작함 </summary>
    private void StartFinalStage()
    {
        currentStage = StageEntry.Final;
        ApplyStageActivation(currentStage); // 모든 타깃 오브젝트 비활성화

        if (titleButton) titleButton.SetActive(false);
        UpdateVideoUIVisible(false); // 서브 디스플레이의 모든 버튼을 비활성화

        // 메인 미션 텍스트 모두 숨김
        foreach (GameObject missionText in missionTextGos)
            SetActiveObject(missionText, false);

        // Sub 6 Text 표시
        SetSubDisplayText(Sub6Index);
        SetActiveObject(playVideoTextGo, false);

        // 마지막 비디오 재생
        PlayVideoByIndex(GetFinalVideoIndex());
    }

    /// <summary> 마지막 비디오가 끝난 후 메인 디스플레이 페이드아웃 및 Sub 7 Text 표시 </summary>
    private IEnumerator Outro()
    {
        // Sub 7 Text 표시
        SetSubDisplayText(Sub7Index);

        // 메인 디스플레이 페이드아웃
        Task fadeTask = FadeManager.Instance.FadeOutMainAsync(FinalMainFadeDuration);
        while (!fadeTask.IsCompleted) yield return null;

        // 정리
        if (videoPlayer) videoPlayer.Stop();
        if (pageVideo) pageVideo.SetActive(false);
        pageVideo = null;
        videoPlayer = null;
        isPlayingVideo = false;

        // 대기
        yield return new WaitForSeconds(outroFadeTime);

        // 타이틀 화면 복귀
        Task titleTask = GameManager.Instance.ShowTitlePageOnly(false);
        while (!titleTask.IsCompleted) yield return null;
    }

    #region Utility

    /// <summary> 스테이지에 따른 타깃 오브젝트 활성화 </summary>
    private void ApplyStageActivation(StageEntry stage)
    {
        if (stage == StageEntry.Final)
        {
            // 모든 타깃 오브젝트 비활성
            foreach (GameObject targetObject in targetObjectsList)
            {
                if (targetObject && targetObject.activeInHierarchy) targetObject.SetActive(false);
            }

            return;
        }

        for (int i = 0; i < targetObjectsList.Count; i++)
        {
            bool active = (i == (int)stage);
            if (targetObjectsList[i] && targetObjectsList[i].activeSelf != active)
                targetObjectsList[i].SetActive(active);
        }
    }

    /// <summary> 스테이지 갱신 후 미션, 서브 텍스트 및 버튼 업데이트 </summary>
    private void UpdateStageUI(StageEntry stage)
    {
        int idx = (int)stage;

        // 메인 텍스트
        for (int i = 0; i < missionTextGos.Count; i++)
            SetActiveObject(missionTextGos[i], stage != StageEntry.Final && i == idx);

        // 서브 텍스트
        for (int i = 0; i < missionSubTextGos.Count; i++)
            SetActiveObject(missionSubTextGos[i], stage != StageEntry.Final && i == idx);

        SetActiveObject(playVideoTextGo, false);

        // 비디오 미재생 시 재생/멈춤, 건너뛰기 버튼 숨김
        if (!isPlayingVideo) UpdateVideoUIVisible(false);
        if (titleButton && stage != StageEntry.Final) titleButton.SetActive(true);
    }

    /// <summary> 비디오 재생 중 서브 디스플레이 UI 전환 </summary>
    private void ChangeSubDisplayOnVideo()
    {
        // 메인 미션 텍스트는 모두 숨김
        foreach (GameObject missionText in missionTextGos)
            SetActiveObject(missionText, false);

        // 허블 ~ 로켓 스테이지
        if (currentStage != StageEntry.Final)
        {
            // 서브 미션 텍스트 숨김 
            foreach (GameObject missionSubText in missionSubTextGos)
                SetActiveObject(missionSubText, false);

            SetActiveObject(playVideoTextGo, true); // 비디오 재생 중 텍스트 활성화
            UpdateVideoUIVisible(true); // 재생/스킵 버튼 보이기
            if (titleButton) titleButton.SetActive(true);
        }
        else // 로켓 이후 마지막 영상 재생 중
        {
            SetActiveObject(playVideoTextGo, false);
            SetSubDisplayText(Sub6Index);

            UpdateVideoUIVisible(false);
            if (titleButton) titleButton.SetActive(false); // 모든 버튼 숨김
        }
    }

    /// <summary> videos 배열의 마지막을 반환함 </summary>
    private int GetFinalVideoIndex()
    {
        return Mathf.Max(0, setting.videos.Length - 1);
    }

    /// <summary> 스테이지 설정 및 세팅 </summary>
    private void SetStage(StageEntry stage)
    {
        currentStage = stage;
        ApplyStageActivation(currentStage);
        UpdateStageUI(currentStage);
    }

    /// <summary> 다음 스테이지로 갱신함 </summary>
    private void NextStage()
    {
        int next = ((int)currentStage + 1) % targetObjectsList.Count; // targetObjectsList.Count == 5
        SetStage((StageEntry)next);
    }

    /// <summary> 머티리얼을 제거 아이콘의 색을 되돌림 </summary>
    private void ClearGrayscaleIcon(int index)
    {
        if (index >= 0 && index < UIManager.Instance.contentsImages.Count)
        {
            GameObject iconGo = UIManager.Instance.contentsImages[index];
            if (iconGo && iconGo.TryGetComponent(out Image img)) img.material = null;
        }
    }

    /// <summary> 게임 오브젝트를 활성/비활성화 함</summary>
    private void SetActiveObject(GameObject go, bool active)
    {
        if (go && go.activeSelf != active) go.SetActive(active);
    }

    /// <summary> 서브 디스플레이 텍스트만 변경함 </summary>
    private void SetSubDisplayText(int subIndex)
    {
        for (int i = 0; i < missionSubTextGos.Count; i++)
            SetActiveObject(missionSubTextGos[i], i == subIndex);
    }

    /// <summary> 서브 디스플레이의 재생/멈춤, 건너뛰기 버튼의 표시를 정함 </summary>
    private void UpdateVideoUIVisible(bool visible)
    {
        if (playPauseButton) playPauseButton.SetActive(visible);
        if (skipButton) skipButton.SetActive(visible);
    }

    private void SetAlpha(GameObject go, float alpha)
    {
        if (!go) return;
        if (go.TryGetComponent(out RawImage raw))
        {
            Color c = raw.color;
            raw.color = new Color(c.r, c.g, c.b, alpha);
        }
    }

    /// <summary> 타이틀로 되돌아가기 전 레퍼런스, 아이콘 등을 초기화 함 </summary>
    public void ResetToFirstStage()
    {
        // 모든 비디오 정지/비활성
        if (videoObjectList != null)
        {
            foreach (GameObject go in videoObjectList)
            {
                if (!go) continue;
                if (go.TryGetComponent(out VideoPlayer vp)) vp.Stop();
                go.SetActive(false);
            }
        }

        pageVideo = null;
        videoPlayer = null;
        isPlayingVideo = false;

        // 서브 디스플레이 버튼 숨김
        if (playPauseButton) playPauseButton.SetActive(false);
        if (skipButton) skipButton.SetActive(false);

        // 아이콘 Grayscale 재적용
        if (UIManager.Instance?.contentsImages != null)
        {
            foreach (GameObject imgGo in UIManager.Instance.contentsImages)
            {
                if (!imgGo) continue;
                if (imgGo.TryGetComponent(out Image img))
                {
                    UICreator.Instance.LoadMaterialAndApply(img, "Materials/M_Grayscale.mat");
                }
            }
        }

        // 스테이지/텍스트 초기화
        currentStage = StageEntry.Hubble;
        ApplyStageActivation(currentStage);
        UpdateStageUI(currentStage);
    }

    #endregion

    #region Fade Method

    /// <summary> 게임 오브젝트의 로우 이미지를 페이드 함 </summary>
    private IEnumerator RawImageFade(GameObject go, float from, float to, float duration)
    {
        if (!go) yield break;
        if (go.TryGetComponent(out RawImage raw))
        {
            float time = 0f;
            while (time < duration)
            {
                float clamp = duration <= 0f ? 1f : Mathf.Clamp01(time / duration);
                float alpha = Mathf.Lerp(from, to, clamp);

                if (raw)
                {
                    Color c = raw.color;
                    raw.color = new Color(c.r, c.g, c.b, alpha);
                }

                time += Time.deltaTime;
                yield return null;
            }

            if (raw) // 마지막 값 보정
            {
                Color c = raw.color;
                raw.color = new Color(c.r, c.g, c.b, to);
            }
        }
    }

    /// <summary> 비디오 오브젝트를 0부터 활성화 후 페이드 인 표시 </summary>
    private IEnumerator FadeInVideo(GameObject go, float duration)
    {
        if (!go) yield break;
        SetAlpha(go, 0f);
        go.SetActive(true);

        yield return StartCoroutine(RawImageFade(go, 0f, 1f, duration));
    }

    /// <summary> 비디오 오브젝트를 1부터 페이드 아웃 후 비활성화 </summary>
    private IEnumerator FadeOutVideo(GameObject go, float duration)
    {
        if (!go || !go.activeSelf) yield break;

        yield return StartCoroutine(RawImageFade(go, 1f, 0f, duration));
        go.SetActive(false);
    }

    #endregion
}