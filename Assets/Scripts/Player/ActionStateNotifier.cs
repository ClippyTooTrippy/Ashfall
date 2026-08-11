using UnityEngine;

namespace SoulsLike.Player
{
    /// <summary>
    /// Attach this directly to the Roll and Attack states in the Animator
    /// (select the state box in the graph -> Add Behaviour -> ActionStateNotifier).
    ///
    /// It calls back into PlayerController the instant the state's exit
    /// transition actually begins, instead of PlayerController guessing
    /// how long the clip takes via a hardcoded duration field.
    ///
    /// exitProgress lets you fire the callback slightly before the transition
    /// completes if you want a little overlap/blend room - 1.0 means "fire
    /// right when the exit transition starts" (matches Roll -> Locomotion's
    /// Exit Time of 0.85, Attack's exit time, etc).
    /// </summary>
    public class ActionStateNotifier : StateMachineBehaviour
    {
        [Tooltip("0-1. Fraction of normalizedTime at which to fire the completion callback. " +
                 "Leave at 1 to fire when the state's own exit transition starts.")]
        [Range(0f, 1f)]
        public float exitProgress = 1f;

        private bool fired;

        public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
        {
            fired = false;
        }

        public override void OnStateUpdate(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
        {
            if (fired) return;

            // normalizedTime can exceed 1 if the state loops or the transition
            // hasn't kicked in yet, so compare against the fractional part.
            float progress = stateInfo.normalizedTime % 1f;
            if (progress <= 0.01f && stateInfo.normalizedTime < 0.5f)
            {
                // Just looped back near 0 - ignore, not a real completion.
                return;
            }

            if (stateInfo.normalizedTime >= exitProgress)
            {
                fired = true;
                NotifyController(animator);
            }
        }

        public override void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
        {
            // Safety net in case OnStateUpdate never crossed the threshold
            // (e.g. exitProgress set higher than the actual configured exit time).
            if (!fired)
            {
                fired = true;
                NotifyController(animator);
            }
        }

        private void NotifyController(Animator animator)
        {
            var controller = animator.GetComponentInParent<PlayerController>();
            if (controller != null)
            {
                controller.OnAnimatorActionComplete();
            }
        }
    }
}