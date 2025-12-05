using UnityEngine;

public class MoonObject : BaseObject
{       
    private void OnEnable()
    {
        var objAnim = GetComponent<ObjectAnimation>();
        if (objAnim != null)
        {
            objAnim.enabled = false;
        }

        Renderer rend = GetComponent<Renderer>();
        if (rend != null)
        {
            // 달 머티리얼 적용
            rend.material = GameManager.Instance.moonMaterial;
            rend.SetPropertyBlock(null);
        }
    }
    
    protected override void PlayVideo()
    {
        GamePage.Instance?.PlayVideoByIndex(1);
    }
}