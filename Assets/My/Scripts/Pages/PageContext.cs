using UnityEngine;

/// <summary> 페이지에서 공통으로 참조할 컨텍스트(카메라, 캔버스 등) </summary>
public sealed class PageContext
{
    public Camera MainCamera { get; }
    public GameObject MainCanvas { get; }
    public GameObject SubCanvas { get; }


    public PageContext(Camera mainCamera, GameObject mainCanvas, GameObject subCanvas)
    {
        MainCamera = mainCamera;
        MainCanvas = mainCanvas;
        SubCanvas = subCanvas;
    }
}

/// <summary>
/// 단일톤/서비스들을 묶어 DI처럼 전달
/// </summary>
public sealed class PageServices
{
    public FadeManager Fade { get; }
    public AudioManager Audio { get; }
    public UICreator UICreator { get; }

    public PageServices(FadeManager fade, AudioManager audio, UICreator ui)
    {
        Fade = fade;
        Audio = audio;
        UICreator = ui;
    }
}