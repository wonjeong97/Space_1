using System;
using System.Collections;
using System.Threading;
//using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using UnityEngine;

[Serializable]
public class TutorialSetting
{
    public float tutorialDisplayTime;

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

    protected override void OnEnable()
    {
        try
        {
            base.OnEnable();
            if (imageTutorial2 && imageTutorial3 && imageAssistance2 && imageAssistance3)
            {
                crosshair1.transform.SetParent(imageTutorial2.transform);
            
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

        isCreated = true;
        SoundManager.Instance?.PlayBGM();
        TutorialSequenceAsync(imageTutorial1, imageTutorial2, imageTutorial3,
                              imageAssistance2, imageAssistance3, token).Forget();
    }

    private async UniTask TutorialSequenceAsync(GameObject tutorial1, GameObject tutorial2, GameObject tutorial3,
                                                GameObject assist1, GameObject assist2, CancellationToken token)
    {   
        int waitMs = Mathf.RoundToInt(setting.tutorialDisplayTime * 1000f);
        tutorial1.SetActive(true);
        await UniTask.Delay(waitMs, DelayType.DeltaTime, PlayerLoopTiming.Update, token);
        
        tutorial1.SetActive(false);
        tutorial2.SetActive(true);
        CrosshairMove(crosshairRT, crosshairRT.anchoredPosition, star2RT.anchoredPosition, 2).Forget();
        
        await UniTask.Delay(waitMs, DelayType.DeltaTime, PlayerLoopTiming.Update, token);
        
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
        
        await UniTask.Delay(TimeSpan.FromSeconds(3), DelayType.DeltaTime, PlayerLoopTiming.Update, token);
        await LoadGamePageAsync(token);
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

    private async UniTask CrosshairMove(RectTransform rt, Vector2 start, Vector2 end, float duration)
    {
        rt.anchoredPosition = start;          

        float time = 0f;
        while (time < duration)
        {
            float p = time / duration;
            rt.anchoredPosition = Vector2.LerpUnclamped(start, end, p);
            time += Time.deltaTime;
            await UniTask.Yield();
        }
        
        rt.anchoredPosition = end;
        if (crosshair1.TryGetComponent(out TutorialCrosshair crosshair))
        {   
            SoundManager.Instance?.PlayFound().Forget();
            crosshair.CrosshairTrigger("Trigger");
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