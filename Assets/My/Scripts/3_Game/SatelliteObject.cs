using UnityEngine;

public class SatelliteObject : BaseObject
{
    private void Start()
    {
        servoAngle = GamePage.Instance?.Setting.servoSatellite;
    }
    
    protected override void PlayVideo()
    {
        ArduinoManager.Instance?.ExcuteCommand(servoAngle);
        GamePage.Instance?.PlayVideoByIndex(2);
    }
}
