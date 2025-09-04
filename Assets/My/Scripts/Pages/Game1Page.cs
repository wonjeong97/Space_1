using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

[Serializable]
public class Game1Setting
{
    public ButtonSetting titleButton;
    public ImageSetting crosshairImage;
    public ImageSetting[] contentsImages;
    public TextSetting missionText;
}

public class Game1Page : BasePage<Game1Setting>
{
    protected override string JsonPath => "JSON/Game1Setting.json";
    private CancellationTokenSource cursorCts;

    protected override async Task BuildContentAsync()
    {
        await UICreator.Instance.CreateSingleButtonAsync(setting.titleButton, mainCanvasObj, CancellationToken.None);
        await UICreator.Instance.CreateSingleImageAsync(setting.crosshairImage, mainCanvasObj, CancellationToken.None);
    }
}