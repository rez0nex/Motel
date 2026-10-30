using UnityEngine;

namespace Motel
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class PrototypeDoor : MonoBehaviour
    {
        [SerializeField, Range(-170f, 170f)] private float openAngle = -95f;
        [SerializeField, Min(1f)] private float degreesPerSecond = 110f;

        private Rigidbody body;
        private Quaternion closedLocalRotation;
        private float currentAngle;
        private bool wantsOpen;

        public string Prompt => wantsOpen ? "E — закрыть" : "E — открыть";

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            closedLocalRotation = transform.localRotation;
        }

        public void Toggle()
        {
            wantsOpen = !wantsOpen;
        }

        private void FixedUpdate()
        {
            currentAngle = Mathf.MoveTowards(currentAngle, wantsOpen ? openAngle : 0f,
                degreesPerSecond * Time.fixedDeltaTime);
            Quaternion localRotation = closedLocalRotation * Quaternion.Euler(0f, currentAngle, 0f);
            Quaternion parentRotation = transform.parent != null ? transform.parent.rotation : Quaternion.identity;
            // The rigidbody sits at the hinge; the solid panel is an offset child collider.
            body.MoveRotation(parentRotation * localRotation);
        }
    }
}
