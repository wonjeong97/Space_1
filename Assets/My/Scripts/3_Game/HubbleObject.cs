using System;
using UnityEngine;

public class HubbleObject : BaseObject
{
    protected override void PlayVideo()
    {
        GamePage.Instance?.PlayVideoByIndex(0);
    }
}