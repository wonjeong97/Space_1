using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

[Serializable]
public class GameSetting
{
    public ImageSetting backgroundImage;
    
    public ButtonSetting titleButton;
    public ButtonSetting playPauseButton;
    public ButtonSetting skipButton;
    
    public ImageSetting crosshairImage;
    public ImageSetting inventoryImage;
    public ImageSetting[] contentsImages;
    public ImageSetting[] cameraImages;
    
    public VideoSetting[] videos;
    public GameObjectSetting[] objects;
    
    public TextSetting missionText;
    public TextSetting subText1;
    public TextSetting subText2;
}

public enum StageEntry
{
    Hubble = 0,
    Moon = 1,
    Satellite = 2,
    Mars = 3,
    Rocket = 4,
}

public class GamePage : BasePage<GameSetting>
{
    public static GamePage Instance { get; private set; }
    
    protected override string JsonPath => "JSON/HubbleSetting.json";

    private GameObject titleButton;
    private GameObject playPauseButton;
    private GameObject skipButton;

    private GameObject subText1;
    private GameObject subText2;
    private GameObject hubbleObject;
    
    private List<GameObject> videoObjectList = new List<GameObject>();
    private List<GameObject> targetObjectsList = new List<GameObject>();
    
    public List<StageEntry> stages = new List<StageEntry>(5);
    private float rayDistance = 100000f;
    private LayerMask hitMask;
    
    private Camera mainCamera;
    
    private void Awake()
    {
        if (Instance == null) Instance = this;
        else if (Instance != this) Destroy(gameObject);
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        
        shouldTurnCamera = true;
        shouldRay = true;
        isPlayingVideo = false;
        
        if (hubbleObject)
            hubbleObject.SetActive(true);
    }

    protected override async Task BuildContentAsync()
    {
        (titleButton, _) = await UICreator.Instance.CreateSingleButtonAsync(setting.titleButton, subCanvasObj, CancellationToken.None);
        if (titleButton.TryGetComponent(out Button button1))
        {
            button1.onClick.AddListener(() => _ = HandleTitleButtonAsync());
        }
        
        (playPauseButton, _) = await UICreator.Instance.CreateSingleButtonAsync(setting.playPauseButton, subCanvasObj, CancellationToken.None);
        if (playPauseButton.TryGetComponent(out Button button2))
        {
            button2.onClick.AddListener(HandlePlayPauseButton);
        }
        playPauseButton.gameObject.SetActive(false);
        
        (skipButton, _) = await UICreator.Instance.CreateSingleButtonAsync(setting.skipButton, subCanvasObj, CancellationToken.None);
        if (skipButton.TryGetComponent(out Button button3))
        {
            button3.onClick.AddListener(() => Debug.Log("skip"));
        }
        skipButton.gameObject.SetActive(false);

        GameObject crosshair = await UICreator.Instance.CreateSingleImageAsync(setting.crosshairImage, mainCanvasObj, CancellationToken.None);
        crosshair.AddComponent<Crosshair>();

        GameObject inventory = await UICreator.Instance.CreateSingleImageAsync(setting.inventoryImage, mainCanvasObj, CancellationToken.None);
        foreach (var image in setting.contentsImages)
        {
            GameObject go = await UICreator.Instance.CreateSingleImageAsync(image, inventory, CancellationToken.None);

            if (go.TryGetComponent(out Image imageComponent))
            {
                UICreator.Instance.LoadMaterialAndApply(imageComponent, "Materials/M_Grayscale.mat");
            }

            UIManager.Instance.contentsImages.Add(go);
        }

        foreach (var cameraImage in setting.cameraImages)
        {
            GameObject go = await UICreator.Instance.CreateSingleImageAsync(cameraImage, inventory, CancellationToken.None);
            UIManager.Instance.cameraImages.Add(go);
            go.SetActive(false);
        }

        await UICreator.Instance.CreateSingleTextAsync(setting.missionText, mainCanvasObj, CancellationToken.None);
        await CreateVideoObject();

        subText1 = await UICreator.Instance.CreateSingleTextAsync(setting.subText1, subCanvasObj, CancellationToken.None);
        subText2 = await UICreator.Instance.CreateSingleTextAsync(setting.subText2, subCanvasObj, CancellationToken.None);
        subText2.SetActive(false);

        await CreateTargetObject();
    }

    private async Task CreateVideoObject()
    {
        foreach (var videoSetting in setting.videos)
        {
            GameObject videoGo = await UICreator.Instance.CreateVideoPlayerAsync(videoSetting, mainCanvasObj, CancellationToken.None);
            videoObjectList.Add(videoGo);
            videoGo.SetActive(false);
        }
    }

    private async Task CreateTargetObject()
    {
        foreach (var goSetting in setting.objects)
        {
            GameObject objectGo = await UICreator.Instance.CreateGameObjectAsync(goSetting, mainCanvasObj, CancellationToken.None);
            targetObjectsList.Add(objectGo);
            objectGo.SetActive(false);
        }
    }

    protected override void ActivateCameraImage(int index)
    {
        if (isCaptured && !UIManager.Instance.cameraImages[index].activeInHierarchy)
        {
            UIManager.Instance.cameraImages[index].gameObject.SetActive(true);
        }
    }

    public void ChangeSubDisplayOnVideo()
    {
        subText1.SetActive(false);
        subText2.SetActive(true);
        
        playPauseButton.gameObject.SetActive(true);
        skipButton.gameObject.SetActive(true);
    }

    private void OnDrawGizmos()
    {
        Ray ray = mainCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));

        // Gizmos 색상 설정
        Gizmos.color = Color.red;

        // 라인 그리기
        Gizmos.DrawLine(ray.origin, ray.origin + ray.direction * rayDistance);

        // Ray 끝 지점을 작은 구체로 표시
        Gizmos.DrawSphere(ray.origin + ray.direction * rayDistance, 0.1f);
    }
}