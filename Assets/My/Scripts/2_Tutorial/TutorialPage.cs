using System;
using System.Collections;
using System.Threading;
//using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

[Serializable]
public class TutorialSetting
{
    //public float tutorialDisplayTime;

    public VideoSetting mainBackground;
    public ImageSetting tutorial1;
    public ImageSetting tutorial2;
    public ImageSetting tutorial3;

    public VideoSetting subBackground;
    public ImageSetting assistance2;
    public ImageSetting assistance3;

    public ImageSetting star1;
    public ImageSetting star2;
    public ImageSetting star3;
    public ImageSetting crosshair1;

    public ImageSetting body;
    public ImageSetting sample;
    public ImageSetting frame;
    
    public ButtonSetting nextButton;    // '다음' 버튼
    public ButtonSetting confirmButton; // '확인' 버튼
}

public class TutorialPage : BasePage<TutorialSetting>
{
    protected override string JsonPath => "JSON/TutorialSetting.json";

    private GameObject hubblePage;

    private GameObject imageTutorial1;
    private GameObject imageTutorial2;
    private GameObject imageTutorial3;

    private GameObject imageAssistance1;
    private GameObject imageAssistance2;
    private GameObject imageAssistance3;

    private GameObject star1;
    private GameObject star2;
    private GameObject star3;
    private GameObject crosshair1;

    private RectTransform crosshairRT;
    private RectTransform star2RT;

    private GameObject body;
    private GameObject sample;
    private GameObject frame;
    
    private GameObject nextButtonObj;
    private GameObject confirmButtonObj;
    private bool isButtonClicked = false;

    protected override void OnEnable()
    {
        try
        {
            base.OnEnable();
            if (imageTutorial2 && imageTutorial3 && imageAssistance2 && imageAssistance3)
            {
                crosshair1.transform.SetParent(imageTutorial2.transform);
                
                isButtonClicked = false;
                if(nextButtonObj) nextButtonObj.SetActive(false);
                if(confirmButtonObj) confirmButtonObj.SetActive(false);
            
                TutorialSequenceAsync(imageTutorial1, imageTutorial2, imageTutorial3,
                                      imageAssistance2, imageAssistance3, cancelToken.Token).Forget();
            }
            
            SoundManager.Instance?.ResumeBgm();
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        
        imageTutorial1.SetActive(true);
        imageTutorial2.SetActive(false);
        imageTutorial3.SetActive(false);
    }

    protected override async UniTask BuildContentAsync(CancellationToken token)
    {
        imageTutorial1 = await UICreator.Instance.CreateSingleImageAsync(setting.tutorial1, mainCanvasObj, token);
        imageTutorial2 = await UICreator.Instance.CreateSingleImageAsync(setting.tutorial2, mainCanvasObj, token);
        imageTutorial3 = await UICreator.Instance.CreateSingleImageAsync(setting.tutorial3, mainCanvasObj, token);
        imageTutorial2.SetActive(false);
        imageTutorial3.SetActive(false);

        imageAssistance2 = await UICreator.Instance.CreateSingleImageAsync(setting.assistance2, subCanvasObj, token);
        imageAssistance3 = await UICreator.Instance.CreateSingleImageAsync(setting.assistance3, subCanvasObj, token);
        imageAssistance3.SetActive(false);

        star1 = await UICreator.Instance.CreateSingleStarAsync(setting.star1, imageTutorial2, token);
        star2 = await UICreator.Instance.CreateSingleStarAsync(setting.star2, imageTutorial2, token);
        star3 = await UICreator.Instance.CreateSingleStarAsync(setting.star3, imageTutorial3, token);
        crosshair1 = await UICreator.Instance.CreateSingleImageAsync(setting.crosshair1, imageTutorial2,  token);    
        crosshair1.AddComponent<TutorialCrosshair>();
        
        crosshair1.TryGetComponent(out crosshairRT);
        star2.TryGetComponent(out star2RT);
        
        sample = await UICreator.Instance.CreateSingleImageAsync(setting.sample, imageTutorial3, token);
        frame = await UICreator.Instance.CreateSingleImageAsync(setting.frame, sample, token);
        sample.transform.localScale = new Vector3(1, 0, 1);
        
        // 버튼 생성 및 이벤트 연결
        (nextButtonObj, _) = await UICreator.Instance.CreateSingleButtonAsync(setting.nextButton, subCanvasObj, token);
        if (nextButtonObj.TryGetComponent(out Button nextBtn))
        {
            nextBtn.onClick.AddListener(() => isButtonClicked = true);
        }
        nextButtonObj.SetActive(false);

        (confirmButtonObj, _) = await UICreator.Instance.CreateSingleButtonAsync(setting.confirmButton, subCanvasObj, token);
        if (confirmButtonObj.TryGetComponent(out Button confirmBtn))
        {
            confirmBtn.onClick.AddListener(() => isButtonClicked = true);
        }
        confirmButtonObj.SetActive(false);

        isCreated = true;
        SoundManager.Instance?.PlayBGM();
        TutorialSequenceAsync(imageTutorial1, imageTutorial2, imageTutorial3,
                              imageAssistance2, imageAssistance3, token).Forget();
    }

    private async UniTask TutorialSequenceAsync(GameObject tutorial1, GameObject tutorial2, GameObject tutorial3,
                                                GameObject assist1, GameObject assist2, CancellationToken token)
    {   
        // 1단계: 이미지 표시 (tutorial 1) -> 다음 버튼 대기
        tutorial1.SetActive(true);
        
        if (nextButtonObj)
        {
            nextButtonObj.SetActive(true);
            await WaitForButtonClick(token);
            nextButtonObj.SetActive(false);
        }

        // 2단계: 크로스헤어 자동 이동 (tutorial 2) -> 이동 완료 후 다음 버튼 대기
        tutorial1.SetActive(false);
        tutorial2.SetActive(true);
        
        // 반복 애니메이션 제어용 토큰 생성
        using (var loopCts = CancellationTokenSource.CreateLinkedTokenSource(token))
        {
            // 반복 애니메이션 시작 (loopCts.Token 사용)
            LoopCrosshairMove(crosshairRT, crosshairRT.anchoredPosition, star2RT.anchoredPosition, 2f, loopCts.Token).Forget();
            
            // 버튼 클릭 대기
            if (nextButtonObj)
            {
                nextButtonObj.SetActive(true);
                await WaitForButtonClick(loopCts.Token);
                nextButtonObj.SetActive(false);
            }
            
            // 버튼을 누르면 루프 애니메이션 취소
            loopCts.Cancel();
        }
        // 3단계: 상호작용 예시 (tutorial 3) -> 확인 버튼 대기
        crosshair1.transform.SetParent(tutorial3.transform);
        crosshair1.transform.position = star3.transform.position;
        
        tutorial2.SetActive(false);
        tutorial3.SetActive(true);

        assist1.SetActive(false);
        assist2.SetActive(true);
        
        if (crosshair1.TryGetComponent(out TutorialCrosshair crosshair))
        {
            crosshair.CrosshairTrigger("Trigger");
        }

        SampleVideoAnim(sample, 1f).Forget();
        
        if (confirmButtonObj)
        {
            confirmButtonObj.SetActive(true);
            await WaitForButtonClick(token);
            confirmButtonObj.SetActive(false);
        }

        // 게임 시작
        await LoadGamePageAsync(token);
    }
    
    private async UniTask LoopCrosshairMove(RectTransform rt, Vector2 start, Vector2 end, float duration, CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested)
            {
                // 위치 초기화
                rt.anchoredPosition = start;
                // 애니메이션 (도중에 토큰 취소되면 멈춤)
                await CrosshairMove(rt, start, end, duration, token);
                // 잠시 대기 (선택 사항)
                
            }
        }
        catch (OperationCanceledException)
        {
            // 루프 취소 시 조용히 종료
        }
    }
    
    // 버튼 클릭 대기 헬퍼 함수
    private async UniTask WaitForButtonClick(CancellationToken token)
    {
        isButtonClicked = false;
        await UniTask.WaitUntil(() => isButtonClicked, cancellationToken: token);
        SoundManager.Instance?.PlayConfirm();
        isButtonClicked = false;
    }

    private async UniTask LoadGamePageAsync(CancellationToken token)
    {
        try
        {
            await FadeManager.Instance.FadeOutAsync(jsonSetting.fadeTime, external: token);
            gameObject.SetActive(false);
            if (hubblePage)
            {
                hubblePage.SetActive(true);
            }
            else
            {
                hubblePage = new GameObject("Game1Page");
                hubblePage.AddComponent<GamePage>();
                    
                UIManager.Instance.pages.Add(hubblePage);
                
                Debug.Log("Create Hubble Page");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[{GetType().Name}] Update failed: {e}");
        }
    }

    private async UniTask CrosshairMove(RectTransform rt, Vector2 start, Vector2 end, float duration, CancellationToken token = default)
    {
        rt.anchoredPosition = start;          

        float time = 0f;
        while (time < duration)
        {   
            token.ThrowIfCancellationRequested();
            
            float p = time / duration;
            rt.anchoredPosition = Vector2.LerpUnclamped(start, end, p);
            time += Time.deltaTime;
            
            await UniTask.Yield(PlayerLoopTiming.Update, token);
        }
        
        rt.anchoredPosition = end;
        if (crosshair1.TryGetComponent(out TutorialCrosshair crosshair))
        {   
            SoundManager.Instance?.PlayFound().Forget();
            crosshair.CrosshairTrigger("Trigger");
            await UniTask.Delay(2000, cancellationToken: token); // 크로스헤어 애니메이션 만큼 대기
            crosshair.CrosshairTrigger("Idle");
            await UniTask.Delay(200, cancellationToken: token);
        }
    }

    private async UniTask SampleVideoAnim(GameObject target, float duration)
    {
        if (!target) return;
        
        Vector3 end = new Vector3(1, 1, 1);

        float elapsed = 0f;
        while (elapsed < duration)
        {
            float t = elapsed / duration;
            float y = Mathf.Lerp(0f, 1f, t);
            
            Vector3 newScale = target.transform.localScale;
            newScale.y = y;
            target.transform.localScale = newScale;
            
            elapsed += Time.deltaTime;
            await UniTask.Yield();
        }
        
        target.transform.localScale = end;
    }
}