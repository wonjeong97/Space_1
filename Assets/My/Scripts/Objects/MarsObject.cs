using UnityEngine;

public class MarsObject : BaseObject
{
    protected override void PlayVideo()
    {
        // GamePage(또는 HubblePage) 싱글턴을 통해 비디오 재생
        if (GamePage.Instance != null)
        {
            GamePage.Instance.PlayVideoByIndex(3);
        }
        else
        {
            Debug.LogError("[HubbleObject] No valid page instance found to play video.");
        }
    }
}
