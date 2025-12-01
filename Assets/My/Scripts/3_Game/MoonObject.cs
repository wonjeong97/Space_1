using UnityEngine;

public class MoonObject : BaseObject
{       
    protected override void PlayVideo()
    {
        GamePage.Instance?.PlayVideoByIndex(1);
    }
}
