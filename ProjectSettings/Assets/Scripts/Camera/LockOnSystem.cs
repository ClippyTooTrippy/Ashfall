using System.Collections.Generic;
using UnityEngine;

namespace SoulsLike.CameraSystem
{
    /// <summary>
    /// Finds and holds a lock-on target among enemies tagged "Enemy".
    /// Toggle with a key; camera and player controller read HasTarget / CurrentTarget.
    /// </summary>
    public class LockOnSystem : MonoBehaviour
    {
        public string enemyTag = "Enemy";
        public float lockOnRadius = 14f;
        public float maxLockAngle = 70f; // degrees from camera forward
        public KeyCode toggleKey = KeyCode.Mouse2; // middle mouse, common souls-like bind

        public Transform CurrentTarget { get; private set; }
        public bool HasTarget => CurrentTarget != null;

        private Transform player;
        private Camera cam;

        private void Awake()
        {
            player = transform;
            cam = Camera.main;
        }

        private void Update()
        {
            if (CurrentTarget != null && !CurrentTarget.gameObject.activeInHierarchy)
                CurrentTarget = null;

            if (Input.GetKeyDown(toggleKey))
            {
                if (HasTarget)
                    CurrentTarget = null;
                else
                    CurrentTarget = FindBestTarget();
            }

            // Drop lock if target wanders too far away.
            if (HasTarget && Vector3.Distance(player.position, CurrentTarget.position) > lockOnRadius * 1.5f)
                CurrentTarget = null;
        }

        private Transform FindBestTarget()
        {
            GameObject[] candidates = GameObject.FindGameObjectsWithTag(enemyTag);
            Transform best = null;
            float bestScore = float.MaxValue;

            foreach (var candidate in candidates)
            {
                Vector3 toCandidate = candidate.transform.position - player.position;
                float distance = toCandidate.magnitude;
                if (distance > lockOnRadius) continue;

                Vector3 viewDir = cam != null ? cam.transform.forward : player.forward;
                float angle = Vector3.Angle(viewDir, toCandidate);
                if (angle > maxLockAngle) continue;

                // Prefer close + centered targets.
                float score = distance + angle * 0.05f;
                if (score < bestScore)
                {
                    bestScore = score;
                    best = candidate.transform;
                }
            }

            return best;
        }
    }
}
