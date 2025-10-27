using System;
using UnityEngine;

public class RocketObject : BaseObject
{
    private void OnEnable()
    {
        GetComponent<Renderer>().material = GameManager.Instance.rocketMaterial;
    }

    private void Start()
    {
        servoAngle = GamePage.Instance?.Setting.servoRocket;
    }
    
    protected override void PlayVideo()
    {
        ArduinoManager.Instance?.ExcuteCommand(servoAngle);
        GamePage.Instance?.PlayVideoByIndex(4);
    }
}
