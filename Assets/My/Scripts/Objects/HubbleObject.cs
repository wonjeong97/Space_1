using UnityEngine;

public class HubbleObject : BaseObject
{
    protected override void PlayVideo()
    {
        Debug.Log("허블 비디오 실행");
        GamePage.Instance.RestartVideoFromStart(0); // 첫 번째 비디오 실행
    }
}