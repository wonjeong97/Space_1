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
        GamePage.Instance?.PlayVideoByIndex(4);
    }
}
