using System;
using System.Threading;
//using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

[Serializable]
public class TitleSetting
{
    public ImageSetting mainBackground;

    public ImageSetting titleImage;
    public ImageSetting titleGuideImage;

    public VideoSetting subBackground;
    public ImageSetting assistance;
    public ButtonSetting confirmButton;
}

public class TitlePage : BasePage<TitleSetting>
{
    private bool inputReady;
    protected override string JsonPath => "JSON/TitleSetting.json";

    private GameObject tutorialPage;

    private GameObject titleBG;
    private GameObject titleImage;
    private GameObject titleGuideImage;
    private GameObject confirmButton;

    protected override async UniTask BuildContentAsync(CancellationToken token)
    {   
        titleBG = await UICreator.Instance.CreateSingleImageAsync(setting.mainBackground, mainCanvasObj, token);
        titleImage = await UICreator.Instance.CreateSingleImageAsync(setting.titleImage, mainCanvasObj, token);
        titleGuideImage = await UICreator.Instance.CreateSingleImageAsync(setting.titleGuideImage, mainCanvasObj, token);
        titleGuideImage.AddComponent<UIBlink>();
        
        await UICreator.Instance.CreateSingleImageAsync(setting.assistance, subCanvasObj, token);

        (confirmButton, _) = await UICreator.Instance.CreateSingleButtonAsync(setting.confirmButton, subCanvasObj, token);
        if (confirmButton.TryGetComponent(out Button button))
        {   
            button.onClick.AddListener(() => OnClickConfirmButtonAsync().Forget());
        }

        isCreated = true;
        GameManager.Instance.TitlePage = gameObject;
    }

    protected override void OnEnable()
    {   
        base.OnEnable();
        ArduinoManager.Instance?.ExcuteCommand("home");
        SoundManager.Instance?.PauseBgm();
        inputReady = true;
        
        if (Camera.main != null && Camera.main.TryGetComponent(out CameraController cc))
        {
            cc.ResetRotation(); // 카메라 회전 0,0,0 으로 초기화
        }
    }

    /// <summary>컨펌 버튼 클릭 시 튜토리얼 페이지로 전환</summary>
    private async UniTask OnClickConfirmButtonAsync()
    {
        if (!inputReady) return;
        inputReady = false;

        try
        {
            Debug.Log("[TitlePage] Confirm Clicked");
            SoundManager.Instance?.PlayConfirm();
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
        catch (Exception e)
        {
            Debug.LogError($"[{GetType().Name}] OnClickConfirmButtonAsync failed: {e}");
        }
    }
}
