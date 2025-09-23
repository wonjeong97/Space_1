using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

[Serializable]
public class TitleSetting
{
    public VideoSetting mainBackground;

    public ImageSetting titleImage;
    public ImageSetting titleGuideImage;

    public VideoSetting subBackground;
    public ImageSetting assistance;
}

public class TitlePage : BasePage<TitleSetting>
{
    private bool inputReady;
    protected override string JsonPath => "JSON/TitleSetting.json";

    private GameObject tutorialPage;

    private GameObject titleImage;
    private GameObject titleGuideImage;

    protected override async Task BuildContentAsync()
    {
        titleImage = await UICreator.Instance.CreateSingleImageAsync(setting.titleImage, mainCanvasObj, CancellationToken.None);
        titleGuideImage = await UICreator.Instance.CreateSingleImageAsync(setting.titleGuideImage, mainCanvasObj, CancellationToken.None);
        titleGuideImage.AddComponent<Blink>();
        
        await UICreator.Instance.CreateSingleImageAsync(setting.assistance, subCanvasObj, CancellationToken.None);

        inputReady = true;

        GameManager.Instance.TitlePage = gameObject;
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
                if (tutorialPage)
                {
                    tutorialPage.SetActive(true);
                    await FadeManager.Instance.FadeInAsync(JsonLoader.Instance.settings.fadeTime);
                }
                else
                {
                    tutorialPage = new GameObject("TutorialPage");
                    tutorialPage.AddComponent<TutorialPage>();
                    UIManager.Instance.pages.Add(tutorialPage);
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[{GetType().Name}] Update failed: {e}");
        }
    }
}