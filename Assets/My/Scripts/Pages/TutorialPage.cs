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
    public ImageSetting miniBackground;

    public TextSetting tutorialText1;
    public ImageSetting tutorialImage1;

    public TextSetting tutorialText2;
    public ImageSetting tutorialImage2;

    public TextSetting infoText;

    public VideoSetting subBackground;
    public TextSetting subText;
}

public class TutorialPage : BasePage<TutorialSetting>
{
    private bool inputReady;
    protected override string JsonPath => "JSON/TutorialSetting.json";

    private GameObject hubblePage;

    private GameObject text1Instance;
    private GameObject image1Instance;

    private GameObject text2Instance;
    private GameObject image2Instance;
    private GameObject infoTextInstance;

    private Coroutine tutorialCoroutine;

    protected override void OnEnable()
    {
        if (text1Instance && image1Instance && text2Instance && image2Instance && infoTextInstance)
        {
            tutorialCoroutine = StartCoroutine(TutorialCoroutine(text1Instance, image1Instance, text2Instance, image2Instance, infoTextInstance));
        }
    }

    protected override void OnDisable()
    {
        StopAllCoroutines();
        tutorialCoroutine = null;

        text1Instance.SetActive(true);
        image1Instance.SetActive(true);

        text2Instance.SetActive(false);
        image2Instance.SetActive(false);
        infoTextInstance.SetActive(false);

        inputReady = false;
    }

    protected override async Task BuildContentAsync()
    {
        GameObject bg = await UICreator.Instance.CreateSingleImageAsync(setting.miniBackground, mainCanvasObj, CancellationToken.None);
        text1Instance = await UICreator.Instance.CreateSingleTextAsync(setting.tutorialText1, bg, CancellationToken.None);
        image1Instance = await UICreator.Instance.CreateSingleImageAsync(setting.tutorialImage1, bg, CancellationToken.None);

        text2Instance = await UICreator.Instance.CreateSingleTextAsync(setting.tutorialText2, bg, CancellationToken.None);
        text2Instance.SetActive(false);

        image2Instance = await UICreator.Instance.CreateSingleImageAsync(setting.tutorialImage2, bg, CancellationToken.None);
        image2Instance.SetActive(false);

        infoTextInstance = await UICreator.Instance.CreateSingleTextAsync(setting.infoText, bg, CancellationToken.None);
        infoTextInstance.SetActive(false);
        infoTextInstance.AddComponent<TextBlink>();

        await UICreator.Instance.CreateSingleTextAsync(setting.subText, subCanvasObj, CancellationToken.None);

        tutorialCoroutine = StartCoroutine(TutorialCoroutine(text1Instance, image1Instance, text2Instance, image2Instance, infoTextInstance));
    }

    private IEnumerator TutorialCoroutine(GameObject text1, GameObject image1, GameObject text2, GameObject image2,
        GameObject infoText)
    {
        yield return new WaitForSeconds(setting.tutorialDisplayTime);

        text1.SetActive(false);
        image1.SetActive(false);

        text2.SetActive(true);
        image2.SetActive(true);
        infoText.SetActive(true);

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