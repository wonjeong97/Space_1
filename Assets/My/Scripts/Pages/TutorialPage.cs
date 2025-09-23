using System;
using System.Collections;
using System.Threading;
using System.Threading.Tasks;
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
    
    private Coroutine tutorialCoroutine;

    protected override void OnEnable()
    {
        if (imageTutorial1 && imageTutorial2 && imageAssistance1 && imageAssistance2)
        {
            StartCoroutine(TutorialCoroutine(imageTutorial1, imageTutorial2, imageAssistance1, imageAssistance2));
        }
    }

    protected override void OnDisable()
    {
        StopAllCoroutines();
        tutorialCoroutine = null;

        imageTutorial1.SetActive(true);
        imageTutorial2.SetActive(false);

        inputReady = false;
    }

    protected override async Task BuildContentAsync()
    {
        imageTutorial1 = await UICreator.Instance.CreateSingleImageAsync(setting.tutorial1, mainCanvasObj, CancellationToken.None);
        imageTutorial2 = await UICreator.Instance.CreateSingleImageAsync(setting.tutorial2, mainCanvasObj, CancellationToken.None);
        imageTutorial2.SetActive(false);
        
        imageAssistance1 = await UICreator.Instance.CreateSingleImageAsync(setting.assistance2, subCanvasObj, CancellationToken.None);
        imageAssistance2 = await UICreator.Instance.CreateSingleImageAsync(setting.assistance3, subCanvasObj, CancellationToken.None);
        imageAssistance2.SetActive(false);

        tutorialCoroutine = StartCoroutine(TutorialCoroutine(imageTutorial1, imageTutorial2, imageAssistance1, imageAssistance2));
    }

    private IEnumerator TutorialCoroutine(GameObject tuto1, GameObject tuto2, GameObject assist1, GameObject assist2)
    {
        yield return new WaitForSeconds(setting.tutorialDisplayTime);

        tuto1.SetActive(false);
        tuto2.SetActive(true);
        
        assist1.SetActive(false);
        assist2.SetActive(true);
        
        inputReady = true;
    }

    protected async void Update()
    {
        try
        {
            if (!inputReady) return;
            if (Input.GetMouseButtonDown(0))
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
        }
        catch (Exception e)
        {
            Debug.LogError($"[{GetType().Name}] Update failed: {e}");
        }
    }
}
