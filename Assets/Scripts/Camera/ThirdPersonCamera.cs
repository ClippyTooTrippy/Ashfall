using UnityEngine;

namespace SoulsLike.CameraSystem
{
    /// <summary>
    /// Orbiting third-person camera. Free-look with the mouse; when a LockOnSystem
    /// target is set, blends toward framing both the player and the target.
    /// </summary>
    public class ThirdPersonCamera : MonoBehaviour
    {
        public Transform target;
        public LockOnSystem lockOn;

        [Header("Orbit")]
        public float distance = 4.5f;
        public float minDistance = 1.2f;
        public float height = 1.6f;
        public float mouseSensitivity = 2.5f;
        public float minPitch = -25f;
        public float maxPitch = 60f;

        [Header("Collision")]
        public LayerMask collisionMask;
        public float collisionRadius = 0.25f;

        private float yaw;
        private float pitch = 15f;

        private void Start()
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            if (target != null) yaw = target.eulerAngles.y;
        }

        private void LateUpdate()
        {
            if (target == null) return;

            if (lockOn != null && lockOn.HasTarget)
                UpdateLockedOrbit();
            else
                UpdateFreeOrbit();

            Vector3 pivot = target.position + Vector3.up * height;
            Quaternion rot = Quaternion.Euler(pitch, yaw, 0f);
            Vector3 desiredPos = pivot - rot * Vector3.forward * distance;

            float actualDistance = distance;
            if (Physics.SphereCast(pivot, collisionRadius, (desiredPos - pivot).normalized, out RaycastHit hit, distance, collisionMask))
                actualDistance = Mathf.Max(minDistance, hit.distance);

            transform.position = pivot - rot * Vector3.forward * actualDistance;
            transform.rotation = rot;
        }

        private void UpdateFreeOrbit()
        {
            yaw += Input.GetAxis("Mouse X") * mouseSensitivity;
            pitch -= Input.GetAxis("Mouse Y") * mouseSensitivity;
            pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
        }

        private void UpdateLockedOrbit()
        {
            Vector3 midpoint = (target.position + lockOn.CurrentTarget.position) * 0.5f;
            Vector3 dir = midpoint - target.position;
            if (dir.sqrMagnitude < 0.001f) return;

            // Look from behind the player toward the midpoint between player and target.
            Vector3 flatDir = target.position - lockOn.CurrentTarget.position;
            flatDir.y = 0f;
            if (flatDir.sqrMagnitude < 0.001f) return;

            Quaternion lookRot = Quaternion.LookRotation(flatDir.normalized, Vector3.up);
            Vector3 desiredEuler = lookRot.eulerAngles;

            yaw = Mathf.LerpAngle(yaw, desiredEuler.y, 8f * Time.deltaTime);
            pitch = Mathf.Lerp(pitch, 20f, 5f * Time.deltaTime);
        }
    }
}
