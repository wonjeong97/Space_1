using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
//using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

[Serializable]
public class GameSetting
{
    public string servoHubble;
    public string servoMoon;
    public string servoSatellite;
    public string servoMars;
    public string servoRocket;

    public float videoFadeTime;
    
    public ImageSetting backgroundImage;

    public ButtonSetting titleButton;

    public ImageSetting playImage;
    public ButtonSetting homeButton;
    public ButtonSetting playButton;
    public ButtonSetting pauseButton;
    public ButtonSetting skipButton;

    public ImageSetting crosshairImage;
    public ImageSetting[] contentsImagesOff;
    public ImageSetting[] contentsImagesOn;

    public VideoSetting[] videos;
    public GameObjectSetting[] objects;

    public ImageSetting[] missionImages;
    public ImageSetting[] missionSubImages;

    public ImageSetting playVideoImage;
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

    private GameObject playImage;
    private GameObject homeButton;
    private GameObject playButton;
    private GameObject pauseButton;
    private GameObject skipButton;

    private readonly List<GameObject> missionGuides = new();
    private readonly List<GameObject> missionSubGuides = new();
    private GameObject playVideoImage;

    // 비디오 / 타겟 오브젝트
    private readonly List<GameObject> videoObjectList = new();
    private readonly List<GameObject> targetObjectsList = new();

    // 현재 단계
    private StageEntry currentStage = StageEntry.Hubble;

    // Final 관련
    private const int Sub6Index = 5;
    private const int Sub7Index = 6;
    private const float FinalMainFadeDuration = 2.5f; // 메인 디스플레이 페이드아웃 시간
    private float videoFadeTime;
    public GameObject MainCanvasObj => mainCanvasObj;

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

    protected override void Start()
    {
        base.Start();

        if (setting != null)
        {
            videoFadeTime = setting.videoFadeTime;
        }
    }

    #endregion

    protected override async UniTask BuildContentAsync(CancellationToken token)
    {   
        // === 서브 디스플레이 버튼 생성 ===
        (titleButton, _) = await UICreator.Instance.CreateSingleButtonAsync(setting.titleButton, subCanvasObj, token);
        if (titleButton.TryGetComponent(out Button button1)) button1.onClick.AddListener(() => HandleTitleButtonAsync(cancelToken.Token).Forget());

        playImage = await UICreator.Instance.CreateSingleImageAsync(setting.playImage, subCanvasObj, token);
        playImage.SetActive(false);

        (homeButton, _) = await UICreator.Instance.CreateSingleButtonAsync(setting.homeButton, playImage, token);
        if (homeButton.TryGetComponent(out Button button2)) button2.onClick.AddListener(() => HandleTitleButtonAsync(cancelToken.Token).Forget());

        (playButton, _) = await UICreator.Instance.CreateSingleButtonAsync(setting.playButton, playImage, token);
        if (playButton.TryGetComponent(out Button button3)) button3.onClick.AddListener(HandlePlayButton);

        (pauseButton, _) = await UICreator.Instance.CreateSingleButtonAsync(setting.pauseButton, playImage, token);
        if (pauseButton.TryGetComponent(out Button button4)) button4.onClick.AddListener(HandlePauseButton);

        (skipButton, _) = await UICreator.Instance.CreateSingleButtonAsync(setting.skipButton, playImage, token);
        if (skipButton.TryGetComponent(out Button button5)) button5.onClick.AddListener(() => HandleSkipButton().Forget());

        // === 크로스헤어 생성 ===
        GameObject crosshair = await UICreator.Instance.CreateSingleImageAsync(setting.crosshairImage, mainCanvasObj, token);
        crosshair.AddComponent<Crosshair>();

        // === 허블, 달, 인공위성, 화성, 로켓 이미지 생성 ===
        foreach (ImageSetting image in setting.contentsImagesOff)
        {
            GameObject go = await UICreator.Instance.CreateSingleImageAsync(image, mainCanvasObj, token);
            UIManager.Instance.contentsImagesOff.Add(go);
        }

        foreach (ImageSetting image in setting.contentsImagesOn)
        {
            GameObject go = await UICreator.Instance.CreateSingleImageAsync(image, mainCanvasObj, token);
            go.SetActive(false);
            UIManager.Instance.contentsImagesOn.Add(go);
        }

        // === 메인,서브 텍스트 및 비디오 재생 텍스트 생성 ===
        // 메인 이미지
        missionGuides.Clear();
        if (setting.missionImages != null)
        {
            foreach (ImageSetting img in setting.missionImages)
            {
                GameObject go = await UICreator.Instance.CreateSingleImageAsync(img, mainCanvasObj, token);
                go.SetActive(false);
                missionGuides.Add(go);
            }
        }

        // 서브 이미지
        missionSubGuides.Clear();
        if (setting.missionSubImages != null)
        {
            foreach (ImageSetting img in setting.missionSubImages)
            {
                GameObject go = await UICreator.Instance.CreateSingleImageAsync(img, subCanvasObj, token);
                if (go.TryGetComponent(out Image subImg)) subImg.raycastTarget = false;
                go.SetActive(false);
                missionSubGuides.Add(go);
            }
        }

        // 비디오 플레이 시 표시하는 "영상 화면이 닫히면 ..." 이미지
        playVideoImage = await UICreator.Instance.CreateSingleImageAsync(setting.playVideoImage, subCanvasObj, token);
        if (playVideoImage.TryGetComponent(out Image playVideoImg)) playVideoImg.raycastTarget = false;
        playVideoImage.SetActive(false);

        // === 스테이지 별 비디오 및 타겟 오브젝트 생성 ===
        await CreateVideoObject(token);
        await CreateTargetObject(token);

        ApplyStageActivation(currentStage);
        UpdateStageUI(currentStage);
        UpdateVideoUIVisible(false);

        isCreated = true;
    }

    #region Sub-Display Button Click Event

    private void HandlePlayButton()
    {
        if (!videoPlayer) return;
        if (!videoPlayer.isPlaying) videoPlayer.Play();
    }

    private void HandlePauseButton()
    {
        if (!videoPlayer) return;
        if (videoPlayer.isPlaying) videoPlayer.Pause();
    }

    private async UniTask HandleTitleButtonAsync(CancellationToken token)
    {   
        SoundManager.Instance?.ResumeBgm();
        await GameManager.Instance.ShowTitlePageOnly(token, true);
    }

    private async UniTask HandleSkipButton()
    {
        if (!isPlayingVideo) return;

        SoundManager.Instance?.PlayCancel();

        // Final은 메인 디스플레이 페이드 로직 사용
        if (currentStage == StageEntry.Final)
        {
            if (videoPlayer) videoPlayer.Stop();
            if (pageVideo)
            {
                pageVideo.SetActive(false);
                pageVideo = null;
            }

            videoPlayer = null;
            isPlayingVideo = false;

            // 마지막 영상 스킵 시에도 BGM 재개
            SoundManager.Instance?.ResumeBgm();

            OnFinalVideoEnded();
            return;
        }

        // 현재 재생 중 비디오 인덱스 (아이콘 컬러 복원용)
        int videoIndex = -1;
        if (pageVideo) videoIndex = videoObjectList.IndexOf(pageVideo);
        else if (videoPlayer) videoIndex = videoObjectList.IndexOf(videoPlayer.gameObject);

        // 정지 및 비활성화
        if (videoPlayer) videoPlayer.Stop();
        if (pageVideo)
        {
            pageVideo.SetActive(false);
            pageVideo = null;
        }

        videoPlayer = null;
        isPlayingVideo = false;

        // 아이콘 컬러 복원(스킵도 완료로 간주)
        GameObject fromGo = UIManager.Instance.contentsImagesOff[videoIndex];
        GameObject toGo = UIManager.Instance.contentsImagesOn[videoIndex];

        if (currentStage == StageEntry.Rocket)
        {
            // 로켓 스킵 → Final 전환 시에도 페이드 연출 적용
            CancellationToken token = this.GetCancellationTokenOnDestroy();
            await StartFinalStageWithFadeAsync(fromGo, toGo, token);
            return;
        }
        
        // 스킵 후 BGM 재생
        if (currentStage != StageEntry.Rocket) SoundManager.Instance?.ResumeBgm();

        // 일반 단계 스킵 → 다음 스테이지
        UpdateVideoUIVisible(false);
        NextStage();
        ApplyStageActivation(currentStage);
        UpdateStageUI(currentStage);
        CrossFadeIcon(fromGo, toGo, 1).Forget();
    }

    #endregion

    #region Create

    /// <summary> 각 스테이지 별 비디오 플레이어 생성 </summary>
    private async UniTask CreateVideoObject(CancellationToken token)
    {
        foreach (VideoSetting videoSetting in setting.videos)
        {
            GameObject videoGo = await UICreator.Instance.CreateVideoPlayerAsync(videoSetting, mainCanvasObj, token, true);
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
    private async UniTask CreateTargetObject(CancellationToken token)
    {
        for (int i = 0; i < setting.objects.Length; i++)
        {
            GameObject objectGo = await UICreator.Instance.CreateGameObjectAsync(setting.objects[i], mainCanvasObj, token);
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

        // 다른 비디오들은 정지 + 비활성화
        for (int i = 0; i < videoObjectList.Count; i++)
        {
            if (i == index) continue;

            GameObject go = videoObjectList[i];
            if (go.TryGetComponent(out VideoPlayer otherVp))
                otherVp.Stop();

            go.SetActive(false);
        }

        GameObject selected = videoObjectList[index];

        // 선택 비디오 활성화 + 알파 0으로 준비
        SetRawAlpha(selected, 0f);
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
    }

    /// <summary> 비디오 종료 시 실행 함수 </summary>
    private async void OnVideoEnded(VideoPlayer vp)
    {
        try
        {
            isPlayingVideo = false;
            
            if (currentStage == StageEntry.Final)
            {
                OnFinalVideoEnded();
                return;
            }

            // 현재 비디오 오브젝트 비활성화
            if (pageVideo)
            {
                if (videoPlayer) videoPlayer.Stop();
                if (currentStage != StageEntry.Rocket)
                {
                    pageVideo.SetActive(false);                    
                }
                pageVideo = null;
                videoPlayer = null;
            }

            // 스테이지 아이콘 색 복원
            int videoIndex = videoObjectList.IndexOf(vp.gameObject);
            GameObject fromGo = UIManager.Instance.contentsImagesOff[videoIndex];
            GameObject toGo = UIManager.Instance.contentsImagesOn[videoIndex];

            if (currentStage == StageEntry.Rocket)
            {
                CancellationToken token = this.GetCancellationTokenOnDestroy();
                await StartFinalStageWithFadeAsync(fromGo, toGo, token);
                return;
            }
            
            // 우주 발사체 영상이 끝나면 엔딩 비디오로 넘어가기 때문에 BGM이 재생되면 안됨
            if (currentStage != StageEntry.Rocket) SoundManager.Instance?.ResumeBgm();
            
            NextStage();                         // 다음 스테이지로 갱신
            ApplyStageActivation(currentStage);  // 스테이지 별 타깃 오브젝트 갱신
            UpdateStageUI(currentStage);         // 미션, 서브 텍스트 업데이트
            UpdateVideoUIVisible(false);         // 재생/멈춤, 건너뛰기 버튼 숨김
            CrossFadeIcon(fromGo, toGo, 1).Forget();
        }
        catch (Exception e)
        {
           Debug.LogError($"[GamePage] OnVideoEnded => Exception: {e}");
        }
    }

    /// <summary> 모든 체험이 끝난 후 처음으로 되돌아감 </summary>
    private void OnFinalVideoEnded()
    {
        // 메인 디스플레이 페이드아웃 + Sub7 + 5초 후 타이틀 복귀
        OutroAsync(this.GetCancellationTokenOnDestroy()).Forget();
    }

    #endregion
    
    /// <summary> Rocket 단계 종료 후 Final 단계로 넘어갈 때: 2초 fade-out → Final 세팅 → 2초 fade-in </summary>
    private async UniTask StartFinalStageWithFadeAsync(GameObject fromGo, GameObject toGo, CancellationToken token)
    {   
        ArduinoManager.Instance?.ExcuteCommand("home");
        
        // 1) Rocket 아이콘 Off -> On 크로스페이드 (다른 스테이지와 동일한 연출)
        if (fromGo != null && toGo != null)
        {
            CrossFadeIcon(fromGo, toGo, 1f).Forget();
        }

        // 2) 메인 디스플레이 페이드아웃 (2초)
        await FadeManager.Instance.FadeOutMainAsync(videoFadeTime, false, token);

        // 3) 실제 Final 스테이지 시작
        StartFinalStage();

        // 4) 메인 디스플레이 페이드인 (2초)
        await FadeManager.Instance.FadeInAsync(videoFadeTime, false, token);
    }
    
    /// <summary> 마지막 스테이지를 시작함 </summary>
    private void StartFinalStage()
    {
        currentStage = StageEntry.Final;
        ApplyStageActivation(currentStage); // 모든 타깃 오브젝트 비활성화

        UpdateVideoUIVisible(false); // 서브 디스플레이의 모든 버튼을 비활성화
        if (titleButton) titleButton.SetActive(false);

        // 메인 미션 텍스트 모두 숨김
        foreach (GameObject missionText in missionGuides)
            SetActiveObject(missionText, false);

        // Sub 6 Text 표시
        SetActiveObject(missionSubGuides[Sub6Index], true);
        SetActiveObject(playVideoImage, false);

        // 마지막 비디오 재생
        PlayVideoByIndex(GetFinalVideoIndex());
    }

    /// <summary> 마지막 비디오가 끝난 후 메인 디스플레이 페이드아웃 및 Sub 7 Text 표시 </summary>
    private async UniTask OutroAsync(CancellationToken token)
    {
        SoundManager.Instance?.ResumeBgm();
        // Sub 7 Text 표시
        SetActiveObject(missionSubGuides[Sub6Index], false);
        SetActiveObject(missionSubGuides[Sub7Index], true);

        // 메인 디스플레이 페이드아웃
        await FadeManager.Instance.FadeOutMainAsync(FinalMainFadeDuration, false, token);

        // 정리
        if (videoPlayer) videoPlayer.Stop();
        if (pageVideo) pageVideo.SetActive(false);
        pageVideo = null;
        videoPlayer = null;
        isPlayingVideo = false;

        // 대기
        int delayMs = Mathf.RoundToInt(outroFadeTime * 1000f);
        await UniTask.Delay(delayMs, DelayType.DeltaTime, PlayerLoopTiming.Update, token);

        // 타이틀 화면 복귀
        await GameManager.Instance.ShowTitlePageOnly(token, false);
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
        for (int i = 0; i < missionGuides.Count; i++)
            SetActiveObject(missionGuides[i], stage != StageEntry.Final && i == idx);

        // 서브 텍스트
        for (int i = 0; i < missionSubGuides.Count; i++)
            SetActiveObject(missionSubGuides[i], stage != StageEntry.Final && i == idx);

        SetActiveObject(playVideoImage, false);

        // 비디오 미재생 시 재생/멈춤, 건너뛰기 버튼 숨김
        if (!isPlayingVideo) UpdateVideoUIVisible(false);
        if (titleButton && stage != StageEntry.Final) titleButton.SetActive(true);
    }

    /// <summary> 비디오 재생 중 서브 디스플레이 UI 전환 </summary>
    private void ChangeSubDisplayOnVideo()
    {
        // 메인 미션 텍스트는 모두 숨김
        foreach (GameObject missionText in missionGuides)
            SetActiveObject(missionText, false);

        // 허블 ~ 로켓 스테이지
        if (currentStage != StageEntry.Final)
        {
            // 서브 미션 텍스트 숨김 
            foreach (GameObject missionSubText in missionSubGuides)
                SetActiveObject(missionSubText, false);

            SetActiveObject(playVideoImage, true); // 비디오 재생 중 텍스트 활성화
            UpdateVideoUIVisible(true); // 재생/스킵 버튼 보이기
        }
        else // 로켓 이후 마지막 영상 재생 중
        {
            SetActiveObject(playVideoImage, false);
            SetActiveObject(missionSubGuides[Sub6Index], true);

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

    /// <summary> 아이콘 On, Off의 크로스 페이드 </summary>
    private async UniTask CrossFadeIcon(GameObject fromGo, GameObject toGo, float duration)
    {
        if (!fromGo || !toGo) return;
        if (!fromGo.TryGetComponent(out Image from) || !toGo.TryGetComponent(out Image to)) return;

        toGo.SetActive(true);
        SetImageAlpha(to, 0f);

        float time = 0f;
        while (time < duration)
        {
            float alpha = time / duration;
            SetImageAlpha(from, 1f - alpha);
            SetImageAlpha(to, alpha);
            time += Time.deltaTime;
            await UniTask.Yield(); // 다음 프레임까지 양보
        }

        SetImageAlpha(from, 0f);
        fromGo.SetActive(false);
        SetImageAlpha(to, 1f);
    }

    /// <summary> 게임 오브젝트를 활성/비활성화 함</summary>
    private void SetActiveObject(GameObject go, bool active)
    {
        if (go && go.activeSelf != active) go.SetActive(active);
    }

    /// <summary> 서브 디스플레이의 재생/멈춤, 건너뛰기 버튼의 표시를 정함 </summary>
    private void UpdateVideoUIVisible(bool visible)
    {
        if (playImage) playImage.SetActive(visible);
        if (titleButton) titleButton.SetActive(!visible);
    }

    private void SetRawAlpha(GameObject go, float alpha)
    {
        if (!go) return;
        if (go.TryGetComponent(out RawImage raw))
        {
            Color c = raw.color;
            raw.color = new Color(c.r, c.g, c.b, alpha);
        }
    }

    private void SetImageAlpha(Image img, float alpha)
    {
        if (!img) return;
        Color c = img.color;
        c.a = alpha;
        img.color = c;
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
        UpdateVideoUIVisible(false);

        // 아이콘 On -> Off
        if (UIManager.Instance?.contentsImagesOff != null && UIManager.Instance.contentsImagesOn != null)
        {
            foreach (GameObject img1 in UIManager.Instance.contentsImagesOn)
            {
                if (!img1) continue;
                img1.SetActive(false);
            }

            foreach (GameObject img2 in UIManager.Instance.contentsImagesOff)
            {
                if (!img2) continue;
                if (img2.TryGetComponent(out Image image)) SetImageAlpha(image, 1f);
                img2.SetActive(true);
            }
        }

        // 스테이지/텍스트 초기화
        currentStage = StageEntry.Hubble;
        ApplyStageActivation(currentStage);
        UpdateStageUI(currentStage);
    }

    #endregion
}