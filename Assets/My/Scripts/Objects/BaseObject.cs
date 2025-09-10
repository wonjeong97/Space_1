using UnityEngine;

public class BaseObject : MonoBehaviour
{
    public virtual void OnRayEnter()
    {
        if (Crosshair.Instance)
        {
            Crosshair.Instance.CrosshairTrigger("Trigger");
        }
    }
    public virtual void OnRayStay(float deltaTime) { }

    public virtual void OnRayExit()
    {
        if (Crosshair.Instance)
        {
            Crosshair.Instance.CrosshairTrigger("Idle");
        }
    }
    private Transform mainCameraTransform;
    
    private void Awake()
    {
        
        if (Camera.main != null)
        {
            mainCameraTransform = Camera.main.transform;
        }
    }
    
    private void LateUpdate()
    {
        if (!mainCameraTransform) return;

        // 카메라 방향으로 회전
        transform.LookAt(mainCameraTransform);
        transform.rotation = Quaternion.LookRotation(transform.position - mainCameraTransform.position);
    }

    /// <summary> 페이지에서 조준이 확정되었을 때 호출됨 </summary>
    public virtual void OnRayConfirmed()
    {
        PlayVideo();
        gameObject.SetActive(false);
    }

    protected virtual void PlayVideo() { }
}