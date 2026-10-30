using UnityEngine;
using UnityEngine.InputSystem;

namespace Motel
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterController))]
    public sealed class FirstPersonController : MonoBehaviour
    {
        [SerializeField] private Camera viewCamera;
        [SerializeField, Min(0.1f)] private float moveSpeed = 3f;
        [SerializeField, Min(0.01f)] private float mouseSensitivity = 0.1f;
        [SerializeField, Range(1f, 89f)] private float pitchLimit = 85f;
        [SerializeField] private float gravity = -20f;

        private CharacterController character;
        private InputActionMap actions;
        private InputAction move;
        private InputAction look;
        private InputAction interact;
        private float verticalSpeed;
        private float pitch;
        private bool hasFocus;

        public bool HasControl => isActiveAndEnabled && hasFocus && Cursor.lockState == CursorLockMode.Locked;
        public bool InteractionPressed => HasControl && interact.WasPressedThisFrame();

        private void Awake()
        {
            character = GetComponent<CharacterController>();
            hasFocus = Application.isFocused;
            actions = new InputActionMap("Prototype");
            move = actions.AddAction("Move", InputActionType.Value, expectedControlLayout: "Vector2");
            move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w")
                .With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a")
                .With("Right", "<Keyboard>/d");
            look = actions.AddAction("Look", InputActionType.Value, "<Mouse>/delta");
            interact = actions.AddAction("Interact", InputActionType.Button, "<Keyboard>/e");

            if (viewCamera == null)
            {
                Debug.LogError("FirstPersonController requires a view camera.", this);
                enabled = false;
            }
        }

        private void OnEnable()
        {
            actions.Enable();
            ReleaseCursor();
        }

        private void OnDisable()
        {
            actions?.Disable();
            ReleaseCursor();
        }

        private void OnDestroy()
        {
            actions?.Dispose();
        }

        private void OnApplicationFocus(bool focused)
        {
            hasFocus = focused;
            if (!focused)
                ReleaseCursor();
        }

        private void Update()
        {
            bool capturedThisFrame = UpdateCursor();
            Vector3 horizontalVelocity = Vector3.zero;
            if (HasControl && !capturedThisFrame)
            {
                // Mouse delta is already a per-frame displacement; do not multiply by deltaTime.
                Vector2 mouseDelta = look.ReadValue<Vector2>() * mouseSensitivity;
                transform.Rotate(0f, mouseDelta.x, 0f);
                pitch = Mathf.Clamp(pitch - mouseDelta.y, -pitchLimit, pitchLimit);
                viewCamera.transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);

                Vector2 input = Vector2.ClampMagnitude(move.ReadValue<Vector2>(), 1f);
                horizontalVelocity = (transform.right * input.x + transform.forward * input.y) * moveSpeed;
            }

            // Gravity continues while the cursor is free; keyboard movement and looking stop.
            if (character.isGrounded && verticalSpeed < 0f)
                verticalSpeed = -2f;
            verticalSpeed = Mathf.Max(verticalSpeed + gravity * Time.deltaTime, -50f);
            character.Move((horizontalVelocity + Vector3.up * verticalSpeed) * Time.deltaTime);
        }

        private bool UpdateCursor()
        {
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                ReleaseCursor();
                return false;
            }

            if (!HasControl && hasFocus && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            {
                Vector2 position = Mouse.current.position.ReadValue();
                if (position.x >= 0f && position.x < Screen.width && position.y >= 0f && position.y < Screen.height)
                {
                    Cursor.lockState = CursorLockMode.Locked;
                    Cursor.visible = false;
                    return true;
                }
            }

            return false;
        }

        private static void ReleaseCursor()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }
}
