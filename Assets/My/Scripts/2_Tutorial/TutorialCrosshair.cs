using System;
using UnityEngine;

public class TutorialCrosshair : MonoBehaviour
{
    public static TutorialCrosshair Instance { get; private set; }

    private RuntimeAnimatorController crosshairAnimator;
    private Animator animator;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        crosshairAnimator = GameManager.Instance.crosshairAnimator;

        if (!gameObject.GetComponent<Animator>())
        {
            gameObject.AddComponent<Animator>();
        }

        animator = gameObject.GetComponent<Animator>();
        animator.runtimeAnimatorController = crosshairAnimator;
    }

    public void CrosshairTrigger(string trigger)
    {
        animator.SetTrigger(trigger);
    }
}