using UnityEngine;
using Cysharp.Threading.Tasks;
using System.Threading;
using System;

public class InteractionController : MonoBehaviour
{
    [Header("Settings")]
    public float dwellThreshold = 0.5f;
    public float maxDistance = 20000f;
    public LayerMask hitMask;
    
    // 상태
    private float dwellTimer;
    private BaseObject currentTarget;
    private bool isInteracting = false; // 줌 인 등 액션 진행 중 여부

    public bool IsRayEnabled { get; set; } = false;
    public Camera MainCamera { get; set; }

    // 이벤트: 타겟이 확정되었을 때 알림
    public event Func<BaseObject, UniTask> OnTargetConfirmed; 

    private void Start()
    {
        if (MainCamera == null) MainCamera = Camera.main;

        var settings = JsonLoader.Instance?.settings;
        if (settings != null)
        {
            dwellThreshold = settings.dwellThreshold;
        }
        
        hitMask = LayerMask.GetMask("Object");
    }

    private void Update()
    {
        if (!IsRayEnabled || isInteracting || !MainCamera) return;

        Ray ray = MainCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        if (Physics.Raycast(ray, out RaycastHit hit, maxDistance, hitMask))
        {
            HandleHit(hit.collider.gameObject);
        }
        else
        {
            ClearTarget();
        }
    }

    private void HandleHit(GameObject hitObj)
    {
        if (hitObj.TryGetComponent(out BaseObject obj))
        {
            if (currentTarget == obj)
            {
                // 계속 응시 중
                currentTarget.OnRayStay(Time.deltaTime);
                dwellTimer += Time.deltaTime;

                if (dwellTimer >= dwellThreshold)
                {
                    ConfirmTarget(obj).Forget();
                }
            }
            else
            {
                // 새로운 타겟 발견
                ClearTarget(); // 기존 타겟 해제
                currentTarget = obj;
                currentTarget.OnRayEnter();
                SoundManager.Instance?.PlayFound();
                dwellTimer = 0f;
            }
        }
        else
        {
            ClearTarget();
        }
    }

    private void ClearTarget()
    {
        if (currentTarget)
        {
            currentTarget.OnRayExit();
            currentTarget = null;
        }
        dwellTimer = 0f;
    }

    private async UniTaskVoid ConfirmTarget(BaseObject obj)
    {
        isInteracting = true; // 중복 실행 방지
        
        if (OnTargetConfirmed != null)
        {
            await OnTargetConfirmed.Invoke(obj);
        }

        // 로직 완료 후 상태 초기화
        isInteracting = false;
        ClearTarget();
    }
}