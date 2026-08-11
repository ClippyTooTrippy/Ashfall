using UnityEngine;

[RequireComponent(typeof(Animator))]
public class PlayerAnimationDriver : MonoBehaviour
{
    private Animator animator;

    private void Awake()
    {
        animator = GetComponent<Animator>();
    }

    public void PlayAttack()
    {
        if (animator != null)
        {
            animator.SetTrigger("Attack");
        }
    }

    public void PlayRoll()
    {
        if (animator != null)
        {
            animator.SetTrigger("Roll");
        }
    }

    public void SetSpeed(float speed)
    {
        if (animator != null)
        {
            animator.SetFloat("Speed", speed, 0.1f, Time.deltaTime);
        }
    }

    public void SetGrounded(bool grounded)
    {
        if (animator != null)
        {
            animator.SetBool("Grounded", grounded);
        }
    }

    public void PlayAnimation(string animationName)
    {
        if (animator != null && !string.IsNullOrEmpty(animationName))
        {
            animator.CrossFade(animationName, 0.1f);
        }
    }
}