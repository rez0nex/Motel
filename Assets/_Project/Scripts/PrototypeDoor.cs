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
        private RoomEightLock access;

        public string Prompt => wantsOpen ? "E — закрыть" : access != null && !access.CanOpen
            ? "E — заперто · нужны ключи № 8" : "E — открыть";
        // NPCs wait outside the swing until the existing door has finished opening.
        public bool IsFullyOpen => wantsOpen && Mathf.Abs(currentAngle - openAngle) < 0.5f;
        public bool IsOpenRequested => wantsOpen;

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            access = GetComponent<RoomEightLock>();
            body.isKinematic = true;
            body.useGravity = false;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            closedLocalRotation = transform.localRotation;
        }

        public void Toggle()
        {
            if (!wantsOpen && access != null && !access.CanOpen) return;
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
