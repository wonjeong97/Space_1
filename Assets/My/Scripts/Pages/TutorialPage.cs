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

    private GameObject game1Page;

    protected override async Task BuildContentAsync()
    {
        GameObject bg = await UICreator.Instance.CreateSingleImageAsync(setting.miniBackground, mainCanvasObj, CancellationToken.None);
        GameObject text1 = await UICreator.Instance.CreateSingleTextAsync(setting.tutorialText1, bg, CancellationToken.None);
        GameObject image1 = await UICreator.Instance.CreateSingleImageAsync(setting.tutorialImage1, bg, CancellationToken.None);
        
        GameObject text2 = await UICreator.Instance.CreateSingleTextAsync(setting.tutorialText2, bg, CancellationToken.None);
        text2.SetActive(false);
        
        GameObject image2 = await UICreator.Instance.CreateSingleImageAsync(setting.tutorialImage2, bg, CancellationToken.None);
        image2.SetActive(false);
        
        GameObject infoText = await UICreator.Instance.CreateSingleTextAsync(setting.infoText, bg, CancellationToken.None);
        infoText.SetActive(false);
        infoText.AddComponent<TextBlink>();
        
        await UICreator.Instance.CreateSingleTextAsync(setting.subText, subCanvasObj, CancellationToken.None);
        
        StartCoroutine(TutorialCoroutine(text1, image1, text2, image2, infoText));
    }

    private IEnumerator TutorialCoroutine(GameObject text1, GameObject image1,  GameObject text2, GameObject image2, GameObject infoText)
    {
        yield return new WaitForSeconds(setting.tutorialDisplayTime);
        
        text1.SetActive(false);
        image1.SetActive(false);
        
        text2.SetActive(true);
        image2.SetActive(true);
        infoText.SetActive(true);

        inputReady = true;
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
                if (game1Page)
                {
                    game1Page.SetActive(true);
                }
                else
                {
                    game1Page = new GameObject("Game1Page");
                    game1Page.AddComponent<Game1Page>();
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[{GetType().Name}] Update failed: {e}");
        }
       
    }
}