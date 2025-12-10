using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
//using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

[Serializable]
public class GameSetting
{
    public ImageSetting arrowLeft;
    public ImageSetting arrowRight;
    public float videoFadeTime;
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
    
    private GameObject arrowLeftObj;
    private GameObject arrowRightObj;

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

    // 자막 디스플레이(싱글톤)
    private SubtitleDisplayer subtitleDisplayer;

    // 처음으로 버튼 딜레이 타임
    private float titleButtonDelayTime = 2.0f;

    public GameObject MainCanvasObj => mainCanvasObj;
    public bool IsPlayingVideo => isPlayingVideo;

    #region Unity Life-cycle

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else if (Instance != this) Destroy(gameObject);
    }

    protected override void OnEnable()
    {
        base.OnEnable();

        if (camController) camController.IsControlEnabled = true;
        if (interactController) interactController.IsRayEnabled = true;

        shouldTurnCamera = true;
        shouldRay = true;
        isPlayingVideo = false;

        ApplyStageActivation(currentStage);
        UpdateStageUI(currentStage);
        UpdateVideoUIVisible(false); // 비디오 미재생 시 버튼 숨김
        
        if (isCreated && currentStage != StageEntry.Final)
        {
            ShowTitleButtonDelayed(titleButtonDelayTime).Forget();
        }
    }

    protected override void OnDisable()
    {
        if (camController) camController.IsControlEnabled = false;
        if (interactController) interactController.IsRayEnabled = false;

        // 홈으로 이동 명령 전송
        ArduinoManager.Instance?.ExcuteCommand("home");

        base.OnDisable();
    }

    protected override void Start()
    {
        base.Start();

        if (camController) camController.IsControlEnabled = true;
        if (interactController) interactController.IsRayEnabled = true;

        if (setting != null)
        {
            videoFadeTime = setting.videoFadeTime;
        }
    }
    
    private void Update()
    {
        // 페이지가 생성되지 않았거나, 비디오 재생 중이거나, 마지막 단계면 화살표 끔
        if (!isCreated || isPlayingVideo || currentStage == StageEntry.Final)
        {
            SetArrowsActive(false, false);
            return;
        }

        UpdateDirectionIndicator();
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
        
        // === 화살표 생성 ===
        arrowLeftObj = await UICreator.Instance.CreateSingleImageAsync(setting.arrowLeft, mainCanvasObj, token);
        arrowLeftObj.AddComponent<UIBlink>();
        if (arrowLeftObj) arrowLeftObj.SetActive(false);

        arrowRightObj = await UICreator.Instance.CreateSingleImageAsync(setting.arrowRight, mainCanvasObj, token);
        arrowRightObj.AddComponent<UIBlink>();
        if (arrowRightObj) arrowRightObj.SetActive(false);

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
        
        // 처음으로 버튼 지연 생성
        if (currentStage != StageEntry.Final)
        {
            ShowTitleButtonDelayed(titleButtonDelayTime).Forget();
        }

        // ===== 자막 Text 생성 및 SubtitleDisplayer 연결 =====
        subtitleDisplayer = SubtitleDisplayer.Instance;
        if (jsonSetting != null && jsonSetting.subtitleOn && subtitleDisplayer != null)
        {
            if (jsonSetting.subtitle1Set != null)
            {
                GameObject go1 = await UICreator.Instance.CreateSingleTextAsync(jsonSetting.subtitle1Set, mainCanvasObj, token);
                if (go1 != null && go1.TryGetComponent(out TextMeshProUGUI tmp1))
                {
                    subtitleText1 = tmp1; // BasePage의 보호 필드 사용
                    subtitleText1.text = string.Empty;
                    subtitleText1.gameObject.SetActive(false);
                    subtitleText1.transform.SetAsLastSibling(); // 항상 맨 앞에 보이도록

                    subtitleDisplayer.FadeTime = jsonSetting.subtitleFadeTime;

                    ApplySubtitleOutline(subtitleText1);
                }
            }

            if (jsonSetting.subtitle2Set != null)
            {
                GameObject go2 = await UICreator.Instance.CreateSingleTextAsync(jsonSetting.subtitle2Set, mainCanvasObj, token);
                if (go2 != null && go2.TryGetComponent(out TextMeshProUGUI tmp2))
                {
                    subtitleText2 = tmp2;
                    subtitleText2.text = string.Empty;
                    subtitleText2.gameObject.SetActive(false);
                    subtitleText2.transform.SetAsLastSibling();

                    ApplySubtitleOutline(subtitleText2);
                }
            }

            subtitleDisplayer.Text = subtitleText1;
            subtitleDisplayer.Text2 = subtitleText2;
        }

        isCreated = true;
    }

    private async UniTaskVoid ShowTitleButtonDelayed(float duration)
    {
        if (!titleButton) return;

        // 일단 즉시 숨김
        titleButton.SetActive(false);

        try
        {
            // 지정된 시간(2초) 대기
            await UniTask.Delay(TimeSpan.FromSeconds(duration), cancellationToken: this.GetCancellationTokenOnDestroy());

            // 2초 후에도 여전히 비디오가 없고, 마지막 스테이지가 아니라면 버튼 표시
            if (!isPlayingVideo && currentStage != StageEntry.Final && gameObject.activeInHierarchy)
            {
                titleButton.SetActive(true);

                CanvasGroup cg = titleButton.GetComponent<CanvasGroup>();
                if (!cg) cg = titleButton.AddComponent<CanvasGroup>();

                cg.alpha = 0f;
                float fadeTime = 0.5f; // 페이드되는 시간
                float elapsed = 0f;

                while (elapsed < fadeTime)
                {
                    // 페이드 도중 버튼이 꺼지거나 페이지가 닫히면 중단
                    if (!titleButton || !titleButton.activeInHierarchy) return;

                    elapsed += Time.deltaTime;
                    cg.alpha = Mathf.Clamp01(elapsed / fadeTime);
                    await UniTask.Yield(PlayerLoopTiming.Update, this.GetCancellationTokenOnDestroy());
                }

                cg.alpha = 1f;
            }
        }
        catch (OperationCanceledException)
        {
            // 페이지가 꺼지거나 파괴되면 무시
        }
    }

    #region Sub-Display Button Click Event

    private void HandlePlayButton()
    {
        if (!videoPlayer) return;
        if (!videoPlayer.isPlaying)
        {
            videoPlayer.Play();
            if (subtitleDisplayer != null)
            {
                subtitleDisplayer.SetPaused(false);
            }
        }
    }

    private void HandlePauseButton()
    {
        if (!videoPlayer) return;
        if (videoPlayer.isPlaying)
        {
            videoPlayer.Pause();
            if (subtitleDisplayer != null)
            {
                subtitleDisplayer.SetPaused(true);
            }
        }
    }

    private async UniTask HandleTitleButtonAsync(CancellationToken token)
    {
        if (subtitleDisplayer != null)
        {
            subtitleDisplayer.StopSubtitle();
        }

        SoundManager.Instance?.PlayConfirm();
        SoundManager.Instance?.ResumeBgm();
        await GameManager.Instance.ShowTitlePageOnly(token, true);
    }

    private async UniTask HandleSkipButton()
    {
        if (!isPlayingVideo) return;

        SoundManager.Instance?.PlayCancel().Forget();

        if (subtitleDisplayer != null)
        {
            subtitleDisplayer.StopSubtitle();
        }

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
            shouldTurnCamera = true;
            shouldRay = true;

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
        shouldTurnCamera = true;
        shouldRay = true;

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

        if (currentStage != StageEntry.Final)
        {
            ShowTitleButtonDelayed(titleButtonDelayTime).Forget();
        }

        CrossFadeIcon(fromGo, toGo, 1).Forget();
    }

    #endregion

    #region Create

    ///<Summary> 각 스테이지 별 비디오 플레이어 생성 </Summary>
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

    ///<Summary> 각 스테이지 별 타겟 오브젝트 생성 </Summary>
    private async UniTask CreateTargetObject(CancellationToken token)
    {
        for (int i = 0; i < setting.objects.Length; i++)
        {
            GameObject objectGo = await UICreator.Instance.CreateGameObjectAsync(setting.objects[i], mainCanvasObj, token);
            switch (i) // 단계별 컴포넌트 부착
            {
                case (int)StageEntry.Hubble:
                    objectGo.AddComponent<HubbleObject>();
                    break;
                case (int)StageEntry.Moon:
                    objectGo.AddComponent<MoonObject>();
                    break;
                case (int)StageEntry.Satellite:
                    objectGo.AddComponent<SatelliteObject>();
                    break;
                case (int)StageEntry.Mars:
                    objectGo.AddComponent<MarsObject>();
                    break;
                case (int)StageEntry.Rocket:
                    objectGo.AddComponent<RocketObject>();
                    break;
            }

            objectGo.SetActive(false);
            targetObjectsList.Add(objectGo);
        }
    }

    #endregion

    #region Video Method

    ///<Summary> 인덱스로 특정 비디오 재생 </Summary>
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

        SetRawAlpha(selected, 0f);
        if (!selected.activeSelf) selected.SetActive(true);

        if (selected.TryGetComponent(out VideoPlayer vp))
        {
            pageVideo = selected;
            videoPlayer = vp;
            videoPlayer.isLooping = false;

            if (!videoPlayer.enabled) videoPlayer.enabled = true;

            videoPlayer.time = 0;
            if (videoPlayer.canSetTime) videoPlayer.frame = 0;

            // 인덱스에 해당하는 VideoSetting을 함께 전달
            VideoSetting vs = null;
            if (setting != null && setting.videos != null && index >= 0 && index < setting.videos.Length)
            {
                vs = setting.videos[index];
            }

            StartCoroutine(PlayVideoAndFadeIn(videoPlayer, selected, vs));
        }
    }

    ///<Summary> 비디오를 페이드인 하고 재생함 + 자막 시작 </Summary>
    private IEnumerator PlayVideoAndFadeIn(VideoPlayer vp, GameObject go, VideoSetting videoSetting)
    {
        if (!go.activeSelf) go.SetActive(true);
        if (!vp.enabled) vp.enabled = true;

        vp.Prepare();
        while (!vp.isPrepared)
            yield return null;

        vp.Play();
        isPlayingVideo = true;
        shouldTurnCamera = false;
        shouldRay = false;

        // 자막 처리 (Settings.subtitleOn 반영)
        if (subtitleDisplayer)
        {
            if (jsonSetting != null && jsonSetting.subtitleOn)
            {
                if (videoSetting == null || string.IsNullOrEmpty(videoSetting.subtitle))
                {
                    subtitleDisplayer.StopSubtitle();
                }
                else
                {
                    subtitleDisplayer.StartSubtitleFromStreamingAssets(videoSetting.subtitle);
                }
            }
            else
            {
                subtitleDisplayer.StopSubtitle();
            }
        }

        ChangeSubDisplayOnVideo();
    }

    ///<Summary> 비디오 종료 시 실행 함수 </Summary>
    private async void OnVideoEnded(VideoPlayer vp)
    {
        try
        {
            isPlayingVideo = false;
            shouldTurnCamera = true;
            shouldRay = true;

            // 자막 정지
            if (subtitleDisplayer != null)
            {
                subtitleDisplayer.StopSubtitle();
            }

            if (currentStage == StageEntry.Final)
            {
                OnFinalVideoEnded();
                return;
            }

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

            int videoIndex = videoObjectList.IndexOf(vp.gameObject);
            GameObject fromGo = UIManager.Instance.contentsImagesOff[videoIndex];
            GameObject toGo = UIManager.Instance.contentsImagesOn[videoIndex];

            if (currentStage == StageEntry.Rocket)
            {
                CancellationToken token = this.GetCancellationTokenOnDestroy();
                await StartFinalStageWithFadeAsync(fromGo, toGo, token);
                return;
            }

            if (currentStage != StageEntry.Rocket) SoundManager.Instance?.ResumeBgm();

            NextStage();
            ApplyStageActivation(currentStage);
            UpdateStageUI(currentStage);
            UpdateVideoUIVisible(false);

            if (currentStage != StageEntry.Final)
            {
                ShowTitleButtonDelayed(titleButtonDelayTime).Forget();
            }

            CrossFadeIcon(fromGo, toGo, 1).Forget();
        }
        catch (Exception e)
        {
            Debug.LogError($"[GamePage] OnVideoEnded -> Exception: {e}");
        }
    }

    ///<Summary> 모든 체험이 끝난 후 처음으로 되돌아감 </Summary>
    private void OnFinalVideoEnded()
    {
        // 메인 디스플레이 페이드아웃 + Sub7 + 5초 후 타이틀 복귀
        OutroAsync(this.GetCancellationTokenOnDestroy()).Forget();
    }

    #endregion

    ///<Summary> Rocket 단계 종료 후 Final 단계로 넘어갈 때: 2초 fade-out -> Final 세팅 -> 2초 fade-in </Summary>
    private async UniTask StartFinalStageWithFadeAsync(GameObject fromGo, GameObject toGo, CancellationToken token)
    {
        ArduinoManager.Instance?.ExcuteCommand("home");

        // 1) Rocket 아이콘 Off -> On 크로스페이드 (다른 스테이지와 동일한 연출)
        if (fromGo != null && toGo != null)
        {
            CrossFadeIcon(fromGo, toGo, 1f).Forget();
        }

        // 2) 메인 디스플레이 페이드아웃
        await FadeManager.Instance.FadeOutMainAsync(videoFadeTime, false, token);

        // 3) 실제 Final 스테이지 시작
        StartFinalStage();

        // 4) 메인 디스플레이 페이드인
        await FadeManager.Instance.FadeInAsync(videoFadeTime, false, token);
    }

    ///<Summary> 마지막 스테이지를 시작함 </Summary>
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

    ///<Summary> 마지막 비디오가 끝난 후 메인 디스플레이 페이드아웃 및 Sub 7 Text 표시 </Summary>
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

    ///<Summary> 스테이지에 따른 타깃 오브젝트 활성화 </Summary>
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

    ///<Summary> 스테이지 갱신 후 미션, 서브 텍스트 및 버튼 업데이트 </Summary>
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

    ///<Summary> 비디오 재생 중 서브 디스플레이 UI 전환 </Summary>
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

    ///<Summary> videos 배열의 마지막을 반환함 </Summary>
    private int GetFinalVideoIndex()
    {
        return Mathf.Max(0, setting.videos.Length - 1);
    }

    ///<Summary> 스테이지 설정 및 세팅 </Summary>
    private void SetStage(StageEntry stage)
    {
        currentStage = stage;
        ApplyStageActivation(currentStage);
        UpdateStageUI(currentStage);
    }

    ///<Summary> 다음 스테이지로 갱신함 </Summary>
    private void NextStage()
    {
        int next = ((int)currentStage + 1) % targetObjectsList.Count; // targetObjectsList.Count == 5
        SetStage((StageEntry)next);
    }

    ///<Summary> 아이콘 On, Off의 크로스 페이드 </Summary>
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
            await UniTask.Yield();
        }

        SetImageAlpha(from, 0f);
        fromGo.SetActive(false);
        SetImageAlpha(to, 1f);
    }

    ///<Summary> 게임 오브젝트를 활성/비활성화 함 </Summary>
    private void SetActiveObject(GameObject go, bool active)
    {
        if (go && go.activeSelf != active) go.SetActive(active);
    }

    ///<Summary> 서브 디스플레이의 재생/멈춤, 건너뛰기 버튼의 표시를 정함 </Summary>
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

    ///<Summary> 타이틀로 되돌아가기 전 레퍼런스, 아이콘 등을 초기화 함 </Summary>
    public void ResetToFirstStage()
    {
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

        if (subtitleDisplayer != null)
        {
            subtitleDisplayer.StopSubtitle();
        }

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

    private void ApplySubtitleOutline(TextMeshProUGUI tmp)
    {
        if (tmp == null) return;

        // 다른 텍스트와 공유하지 않도록 머티리얼 인스턴스 생성
        Material mat = Instantiate(tmp.fontMaterial);
        tmp.fontMaterial = mat;

        // Outline 키워드 활성화
        tmp.fontMaterial.EnableKeyword("OUTLINE_ON");

        // Settings에 값이 있다면 사용, 없으면 기본값
        float width = 0.2f;
        Color color = Color.black;

        // 외곽선 두께/색 설정
        tmp.fontMaterial.SetFloat(ShaderUtilities.ID_OutlineWidth, width);
        tmp.fontMaterial.SetColor(ShaderUtilities.ID_OutlineColor, color);
    }
    
    private void UpdateDirectionIndicator()
    {
        if (targetObjectsList == null || targetObjectsList.Count <= (int)currentStage) return;
        GameObject target = targetObjectsList[(int)currentStage];
        
        if (!target) return;

        Camera cam = Camera.main;
        if (!cam) return;

        // 1. 화면 안에 있는지 판단 (상하 무시, 좌우만)
        Vector3 viewPos = cam.WorldToViewportPoint(target.transform.position);
        bool isHorizontallyOnScreen = viewPos.z > 0 && 
                                      viewPos.x >= 0f && viewPos.x <= 1f;

        if (isHorizontallyOnScreen)
        {
            SetArrowsActive(false, false);
        }
        else
        {
            // 2. [수정] 절대 각도 비교 방식으로 변경 (최단거리 로직 제거)
            
            // 타겟의 방향 벡터 계산
            Vector3 dir = target.transform.position - cam.transform.position;
            
            // 타겟의 Yaw 각도 산출 (-180 ~ 180도)
            // Atan2(x, z)는 Unity 좌표계에서 Yaw 각도와 일치합니다.
            float targetYaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;

            // 카메라의 현재 Yaw 각도 (-180 ~ 180도로 정규화)
            float camYaw = cam.transform.eulerAngles.y;
            if (camYaw > 180f) camYaw -= 360f;

            // 단순 수치 비교 (회전 제한이 있으므로 선형적으로 판단)
            if (targetYaw < camYaw) 
            {
                // 타겟 각도가 현재보다 작으므로 "왼쪽"으로 가야 함
                // 단, 왼쪽 한계에 도달했다면 표시하지 않음
                bool canTurnLeft = camController != null && !camController.IsAtLeftLimit;
                SetArrowsActive(canTurnLeft, false);
            }
            else 
            {
                // 타겟 각도가 현재보다 크므로 "오른쪽"으로 가야 함
                // 단, 오른쪽 한계에 도달했다면 표시하지 않음
                bool canTurnRight = camController != null && !camController.IsAtRightLimit;
                SetArrowsActive(false, canTurnRight);
            }
        }
    }

    private void SetArrowsActive(bool left, bool right)
    {
        if (arrowLeftObj && arrowLeftObj.activeSelf != left) arrowLeftObj.SetActive(left);
        if (arrowRightObj && arrowRightObj.activeSelf != right) arrowRightObj.SetActive(right);
    }
    
    public void ForceStopAllVideos()
    {
        // 1. 현재 재생 변수 해제
        isPlayingVideo = false;
        videoPlayer = null;
        pageVideo = null;

        // 2. 모든 비디오 플레이어 정지 및 비활성화
        if (videoObjectList != null)
        {
            foreach (var go in videoObjectList)
            {
                if (go && go.TryGetComponent(out VideoPlayer vp))
                {
                    if (vp.isPlaying) vp.Stop();
                    vp.enabled = false; // 컴포넌트를 꺼서 확실하게 중단
                }
            }
        }
    }
}