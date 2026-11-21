using UnityEngine;

namespace Motel
{
    [DisallowMultipleComponent]
    public sealed class TechnicalHatch : MonoBehaviour
    {
        [SerializeField] private MotelLoopController loop;
        [SerializeField] private Transform leaf;
        [SerializeField] private Vector3 axis = Vector3.up;
        [SerializeField] private float openAngle = 170;
        [SerializeField] private string label = "шкаф управления";
        private Quaternion closed = Quaternion.identity;
        public bool IsOpen { get; private set; }
        public bool IsFullyOpen => IsOpen && Quaternion.Angle(leaf.localRotation, Target) < .5f;
        public Transform Leaf => leaf;
        public Quaternion OpenRotation => closed * Quaternion.AngleAxis(openAngle, axis);
        private Quaternion Target => IsOpen ? OpenRotation : closed;
        public string Prompt => "E — " + (IsOpen ? "закрыть " : "открыть ") + label;
        public void Configure(MotelLoopController owner, Transform movingLeaf, Vector3 rotationAxis, float angle, string title)
        { loop = owner; leaf = movingLeaf; axis = rotationAxis; openAngle = angle; label = title; }
        private void Awake() { closed = leaf.localRotation; IsOpen = false; }
        public void Interact()
        { if (loop != null && !loop.IsPaused && !loop.SuppressInteractionUI) IsOpen = !IsOpen; }
        private void Update()
        {
            if (loop == null || loop.IsPaused || loop.Transitioning) return;
            leaf.localRotation = Quaternion.RotateTowards(leaf.localRotation, Target, 115 * Time.deltaTime);
        }
    }
}
