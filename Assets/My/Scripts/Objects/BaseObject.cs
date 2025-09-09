using UnityEngine;

public class BaseObject : MonoBehaviour
{
    public virtual void OnRayEnter() { }
    public virtual void OnRayStay(float deltaTime) { }
    public virtual void OnRayExit() { }

    /// <summary>
    /// 페이지에서 3초 조준이 확정되었을 때 호출됨
    /// </summary>
    public virtual void OnRayConfirmed()
    {
        PlayVideo();
        gameObject.SetActive(false);
    }

    protected virtual void PlayVideo() { }
}