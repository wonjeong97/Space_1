using UnityEngine;

public class MarsObject : BaseObject
{   
    private void Start()
    {
        servoAngle = GamePage.Instance?.Setting.servoMars;
    }
   
    protected override void PlayVideo()
    {   
        ArduinoManager.Instance?.ExcuteCommand(servoAngle);
        GamePage.Instance?.PlayVideoByIndex(3);
    }
}
