using UnityEngine;

public class BaseObject : MonoBehaviour
{
    private float rayHitTime;

    public virtual void OnRayEnter() => rayHitTime = 0f;

    public virtual void OnRayStay(float deltaTime)
    {
        rayHitTime += deltaTime;
        if (rayHitTime >= 2f)
        {
            PlayVideo();
            gameObject.SetActive(false); // 비디오 실행 후 오브젝트 비활성화
        }
    }

    public virtual void OnRayExit() => rayHitTime = 0f;

    protected virtual void PlayVideo() { }
}