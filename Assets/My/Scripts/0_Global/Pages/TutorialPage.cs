using System;
using System.Collections;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor.PackageManager.UI;
using UnityEngine;

[Serializable]
public class TutorialSetting
{
    public float tutorialDisplayTime;

    public VideoSetting mainBackground;
    public ImageSetting tutorial1;
    public ImageSetting tutorial2;

    public VideoSetting subBackground;
    public ImageSetting assistance2;
    public ImageSetting assistance3;

    public ImageSetting star1;
    public ImageSetting star2;
    public ImageSetting star3;
    public ImageSetting crosshair1;

    public ImageSetting sample;
    public ImageSetting frame;
}

public class TutorialPage : BasePage<TutorialSetting>
{
    private bool inputReady;
    protected override string JsonPath => "JSON/TutorialSetting.json";

    private GameObject hubblePage;

    private GameObject imageTutorial1;
    private GameObject imageTutorial2;

    private GameObject imageAssistance1;
    private GameObject imageAssistance2;

    private GameObject star1;
    private GameObject star2;
    private GameObject star3;
    private GameObject crosshair1;

    private RectTransform crosshairRT;
    private RectTransform star2RT;

    private GameObject sample;
    private GameObject frame;

    private Coroutine tutorialCoroutine;

    protected override void OnEnable()
    {
        if (imageTutorial1 && imageTutorial2 && imageAssistance1 && imageAssistance2)
        {
            inputReady = false;
            crosshair1.transform.SetParent(imageTutorial1.transform);
            
            StartCoroutine(TutorialCoroutine(imageTutorial1, imageTutorial2, imageAssistance1, imageAssistance2));
        }
    }

    protected override void OnDisable()
    {
        StopAllCoroutines();
        tutorialCoroutine = null;

        imageTutorial1.SetActive(true);
        imageTutorial2.SetActive(false);
    }

    protected override async Task BuildContentAsync()
    {
        imageTutorial1 = await UICreator.Instance.CreateSingleImageAsync(setting.tutorial1, mainCanvasObj, CancellationToken.None);
        imageTutorial2 = await UICreator.Instance.CreateSingleImageAsync(setting.tutorial2, mainCanvasObj, CancellationToken.None);
        imageTutorial2.SetActive(false);

        imageAssistance1 = await UICreator.Instance.CreateSingleImageAsync(setting.assistance2, subCanvasObj, CancellationToken.None);
        imageAssistance2 = await UICreator.Instance.CreateSingleImageAsync(setting.assistance3, subCanvasObj, CancellationToken.None);
        imageAssistance2.SetActive(false);

        star1 = await UICreator.Instance.CreateSingleStarAsync(setting.star1, imageTutorial1, CancellationToken.None);
        star2 = await UICreator.Instance.CreateSingleStarAsync(setting.star2, imageTutorial1, CancellationToken.None);
        star3 = await UICreator.Instance.CreateSingleStarAsync(setting.star3, imageTutorial2, CancellationToken.None);
        crosshair1 = await UICreator.Instance.CreateSingleImageAsync(setting.crosshair1, imageTutorial1,  CancellationToken.None);    
        crosshair1.AddComponent<TutorialCrosshair>();
        
        crosshair1.TryGetComponent(out crosshairRT);
        star2.TryGetComponent(out star2RT);
        
        sample = await UICreator.Instance.CreateSingleImageAsync(setting.sample, imageTutorial2, CancellationToken.None);
        frame = await UICreator.Instance.CreateSingleImageAsync(setting.frame, sample, CancellationToken.None);
        sample.transform.localScale = new Vector3(1, 0, 1);
        
        tutorialCoroutine = StartCoroutine(TutorialCoroutine(imageTutorial1, imageTutorial2, imageAssistance1, imageAssistance2));
    }

    private IEnumerator TutorialCoroutine(GameObject tuto1, GameObject tuto2, GameObject assist1, GameObject assist2)
    {   
        _ = CrosshairMove(crosshairRT, crosshairRT.anchoredPosition, star2RT.anchoredPosition, 2);
        yield return new WaitForSeconds(setting.tutorialDisplayTime);
        
        crosshair1.transform.SetParent(tuto2.transform);
        crosshair1.transform.position = star3.transform.position;
        
        tuto1.SetActive(false);
        tuto2.SetActive(true);

        assist1.SetActive(false);
        assist2.SetActive(true);
        
        if (crosshair1.TryGetComponent(out TutorialCrosshair crosshair))
        {
            crosshair.CrosshairTrigger("Trigger");
        }

        _ = SampleVideoAnim(sample, 1f);
        
        yield return new WaitForSeconds(3f);
        LoadGamePage();
        inputReady = true;
    }

    private async void LoadGamePage()
    {
        try
        {
            await FadeManager.Instance.FadeOutAsync(jsonSetting.fadeTime);
            gameObject.SetActive(false);
            if (hubblePage)
            {
                hubblePage.SetActive(true);
                await FadeManager.Instance.FadeInAsync(JsonLoader.Instance.settings.fadeTime);
            }
            else
            {
                hubblePage = new GameObject("Game1Page");
                hubblePage.AddComponent<GamePage>();
                    
                UIManager.Instance.pages.Add(hubblePage);
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[{GetType().Name}] Update failed: {e}");
        }
    }

    private async Task CrosshairMove(RectTransform rt, Vector2 start, Vector2 end, float duration)
    {
        rt.anchoredPosition = start;          

        float time = 0f;
        while (time < duration)
        {
            float p = time / duration;
            rt.anchoredPosition = Vector2.LerpUnclamped(start, end, p);
            time += Time.deltaTime;
            await Task.Yield();
        }
        
        rt.anchoredPosition = end;
        if (crosshair1.TryGetComponent(out TutorialCrosshair crosshair))
        {
            crosshair.CrosshairTrigger("Trigger");
        }
    }

    private async Task SampleVideoAnim(GameObject target, float duration)
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
            await Task.Yield();
        }
        
        target.transform.localScale = end;
    }
}