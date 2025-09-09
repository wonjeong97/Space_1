using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

[Serializable]
public class TitleSetting
{
    public VideoSetting mainBackground;
    public TextSetting titleText;
    public TextSetting infoText;

    public VideoSetting subBackground;
    public TextSetting subText;
}

public class TitlePage : BasePage<TitleSetting>
{
    private bool inputReady;
    protected override string JsonPath => "JSON/TitleSetting.json";

    private GameObject tutorialPage;

    protected override async Task BuildContentAsync()
    {
        await UICreator.Instance.CreateSingleTextAsync(setting.titleText, mainCanvasObj, CancellationToken.None);

        GameObject infoText =
            await UICreator.Instance.CreateSingleTextAsync(setting.infoText, mainCanvasObj, CancellationToken.None);
        infoText.AddComponent<TextBlink>();

        await UICreator.Instance.CreateSingleTextAsync(setting.subText, subCanvasObj, CancellationToken.None);

        inputReady = true;

        GameManager.Instance.TitlePage = gameObject;
    }

    private async void Update()
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
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[{GetType().Name}] Update failed: {e}");
        }
    }
}