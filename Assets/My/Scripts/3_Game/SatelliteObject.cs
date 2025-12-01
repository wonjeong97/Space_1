using UnityEngine;

public class SatelliteObject : BaseObject
{
    protected override void PlayVideo()
    {
        GamePage.Instance?.PlayVideoByIndex(2);
    }
}
