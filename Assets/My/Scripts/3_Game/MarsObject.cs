using UnityEngine;

public class MarsObject : BaseObject
{   
    protected override void PlayVideo()
    {   
        GamePage.Instance?.PlayVideoByIndex(3);
    }
}
