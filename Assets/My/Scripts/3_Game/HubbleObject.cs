using System;
using UnityEngine;

public class HubbleObject : BaseObject
{
    private void Start()
    {
        servoAngle = GamePage.Instance?.Setting.servoHubble;
    }

    protected override void PlayVideo()
    {
        ArduinoManager.Instance?.ExcuteCommand(servoAngle);
        GamePage.Instance?.PlayVideoByIndex(0);
    }
}