using UnityEngine;

namespace Motel
{
    [DisallowMultipleComponent, RequireComponent(typeof(Rigidbody), typeof(BoxCollider))]
    public sealed class SlidingWardrobe : MonoBehaviour
    {
        [SerializeField] private MotelLoopController loop;
        [SerializeField] private Vector3 displacement = new Vector3(0, 0, 1.23f);
        [SerializeField, Min(0.05f)] private float speed = 0.6f;
        private Rigidbody body;
        private BoxCollider solid;
        private Vector3 destination;
        private bool requested;
        private CharacterController[] characters;
        public bool IsMoved { get; private set; }
        public bool WaitingForClearance { get; private set; }
        public string BlockingObject { get; private set; }
        public string Prompt => IsMoved ? "Шкаф сдвинут · проход свободен" : requested
            ? WaitingForClearance ? "Освободите место для шкафа" : "Шкаф сдвигается…" : "E — сдвинуть шкаф";

        public void Configure(MotelLoopController controller) => loop = controller;
        private void Awake()
        {
            body = GetComponent<Rigidbody>(); solid = GetComponent<BoxCollider>();
            body.isKinematic = true; body.useGravity = false;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            destination = body.position + displacement;
            characters = FindObjectsByType<CharacterController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            requested = IsMoved = WaitingForClearance = false;
        }
        public void Interact()
        {
            if (loop == null || loop.IsPaused || loop.Transitioning || loop.SuppressInteractionUI) return;
            requested = true;
        }
        private void FixedUpdate()
        {
            if (!requested || IsMoved || loop.IsPaused || loop.Transitioning) return;
            Vector3 step = Vector3.MoveTowards(body.position, destination, speed * Time.fixedDeltaTime) - body.position;
            WaitingForClearance = false;
            BlockingObject = null;
            Vector3 center = body.position + body.rotation * solid.center;
            Vector3 clearance = solid.size / 2 + new Vector3(0.035f, 0, 0.035f);
            // CharacterController.Move and rigidbody interpolation can expose different query poses
            // within a frame. Check the actual upright capsule position before a kinematic move too.
            foreach (var character in characters)
            {
                if (character == null || !character.enabled || !character.gameObject.activeInHierarchy ||
                    character.gameObject.scene != gameObject.scene) continue;
                Vector3 point = Quaternion.Inverse(body.rotation) * (character.transform.TransformPoint(character.center) - center - step);
                float dx = Mathf.Max(Mathf.Abs(point.x) - solid.size.x / 2, 0);
                float dz = Mathf.Max(Mathf.Abs(point.z) - solid.size.z / 2, 0);
                float radius = character.radius + character.skinWidth + 0.035f;
                if (Mathf.Abs(point.y) < solid.size.y / 2 + character.height / 2 && dx * dx + dz * dz < radius * radius)
                { WaitingForClearance = true; BlockingObject = character.name; return; }
            }
            foreach (var other in Physics.OverlapBox(center + step, clearance, body.rotation, ~0, QueryTriggerInteraction.Ignore))
                if (!other.transform.IsChildOf(transform)) { WaitingForClearance = true; BlockingObject = other.name + " overlap"; return; }
            // Do not push the player/NPC into a wall or slide through another solid object.
            // Query the authoritative physics pose, not the interpolated render transform.
            foreach (var hit in Physics.BoxCastAll(center, solid.size / 2,
                         step.normalized, body.rotation, step.magnitude + 0.015f, ~0, QueryTriggerInteraction.Ignore))
                if (!hit.collider.transform.IsChildOf(transform)) { WaitingForClearance = true; BlockingObject = hit.collider.name + " sweep"; return; }
            Vector3 next = body.position + step;
            body.MovePosition(next);
            IsMoved = Vector3.Distance(next, destination) < 0.001f;
        }
    }
}
