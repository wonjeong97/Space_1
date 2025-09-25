using System;
using UnityEngine;

public class RocketObject : BaseObject
{
    private void OnEnable()
    {
        GetComponent<Renderer>().material = GameManager.Instance.rocketMaterial;
    }

    protected override void PlayVideo()
    {
        if (GamePage.Instance)
        {
            GamePage.Instance.PlayVideoByIndex(4);
        }
        else
        {
            Debug.LogError("[HubbleObject] No valid page instance found to play video.");
        }
    }
}
