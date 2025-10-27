using UnityEngine;

public class MoonObject : BaseObject
{       
    private void Start()
    {
        servoAngle = GamePage.Instance?.Setting.servoMoon;
    }
    
    protected override void PlayVideo()
    {
        ArduinoManager.Instance?.ExcuteCommand(servoAngle);
        GamePage.Instance?.PlayVideoByIndex(1);
    }
}
