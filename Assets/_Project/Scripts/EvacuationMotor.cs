using UnityEngine;

namespace Motel
{
    // Per-character, per-scene movement. No shared rescue state or navigation singleton.
    public sealed class EvacuationMotor
    {
        public readonly struct Gate
        {
            public readonly int Waypoint;
            public readonly PrototypeDoor Door;
            public Gate(int waypoint, PrototypeDoor door) { Waypoint = waypoint; Door = door; }
        }

        private readonly CharacterController character;
        private readonly MotelLoopController loop;
        private readonly DialogueController dialogue;
        private readonly Object participant;
        private readonly Vector3[] route;
        private readonly Transform destination;
        private readonly Gate[] gates;
        private readonly float speed;
        private float verticalSpeed;
        private bool walking;
        public int Waypoint { get; private set; }
        public bool WaitingForPath { get; private set; }
        public bool Arrived { get; private set; }
        public string BlockingObject { get; private set; }

        public EvacuationMotor(CharacterController body, MotelLoopController controller, DialogueController window,
            Object owner, Vector3[] path, Transform safePoint, float moveSpeed, params Gate[] doors)
        {
            character = body; loop = controller; dialogue = window; participant = owner;
            route = path; destination = safePoint; speed = moveSpeed; gates = doors;
        }

        public void Begin() => walking = true;
        public void ReportCollision(ControllerColliderHit hit)
        {
            if (Mathf.Abs(hit.normal.y) < 0.7f) BlockingObject = hit.collider.name;
        }

        // Returns true exactly once, on physical arrival, not when the route is assigned.
        public bool Tick()
        {
            if (!character.enabled || !character.gameObject.activeInHierarchy || loop.IsPaused || loop.Transitioning) return false;
            if (character.isGrounded && verticalSpeed < 0) verticalSpeed = -2;
            verticalSpeed = Mathf.Max(-30, verticalSpeed - 20 * Time.deltaTime);
            character.Move(Vector3.up * verticalSpeed * Time.deltaTime);
            if (!walking || Arrived || dialogue.IsOpen && dialogue.Participant == participant) return false;
            foreach (Gate gate in gates)
                if (Waypoint == gate.Waypoint && gate.Door != null && !gate.Door.IsFullyOpen)
                {
                    WaitingForPath = true;
                    BlockingObject = gate.Door.name;
                    return false;
                }

            Vector3 delta = route[Waypoint] - character.transform.position;
            delta.y = 0;
            if (delta.magnitude < 0.08f)
            {
                if (Waypoint < route.Length - 1) Waypoint++;
                else if (Vector2.Distance(new Vector2(character.transform.position.x, character.transform.position.z),
                             new Vector2(destination.position.x, destination.position.z)) < 0.45f &&
                         Mathf.Abs(character.transform.position.y - destination.position.y) < 0.5f)
                {
                    Arrived = true;
                    WaitingForPath = false;
                    BlockingObject = null;
                    return true;
                }
                return false;
            }
            Vector3 direction = delta.normalized;
            float step = Mathf.Min(speed * Time.deltaTime, delta.magnitude);
            Vector3 center = character.transform.position + character.center;
            // Test the torso, leaving foot clearance above the floor.
            float end = character.height / 2 - character.radius - 0.08f;
            var hits = Physics.CapsuleCastAll(center - Vector3.up * end, center + Vector3.up * end,
                character.radius + 0.035f, direction, step + 0.04f, ~0, QueryTriggerInteraction.Ignore);
            WaitingForPath = false;
            BlockingObject = null;
            foreach (RaycastHit hit in hits)
            {
                if (hit.collider.transform.IsChildOf(character.transform)) continue;
                WaitingForPath = true;
                BlockingObject = hit.collider.name;
                return false;
            }
            Vector3 previous = character.transform.position;
            character.Move(direction * step);
            Vector3 movement = character.transform.position - previous;
            movement.y = 0;
            WaitingForPath = movement.sqrMagnitude < step * step * 0.1f;
            if (!WaitingForPath)
                character.transform.rotation = Quaternion.RotateTowards(character.transform.rotation,
                    Quaternion.LookRotation(direction), 180 * Time.deltaTime);
            return false;
        }
    }
}
