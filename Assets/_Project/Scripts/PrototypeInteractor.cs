using UnityEngine;

namespace Motel
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(10)]
    public sealed class PrototypeInteractor : MonoBehaviour
    {
        [SerializeField] private FirstPersonController player;
        [SerializeField] private Camera viewCamera;
        [SerializeField, Range(0.1f, 2.5f)] private float interactionDistance = 2.5f;

        private PrototypeDoor target;
        private EmergencyCutoff cutoff;
        private LeonidEvacuee leonid;
        private CanePickup cane;
        private LeonidEvacuee episode;
        private VadimEvacuee vadim;
        private EquipmentInspection inspection;
        private MotelLoopController loop;
        private GUIStyle labelStyle;

        private void Start()
        {
            loop = FindAnyObjectByType<MotelLoopController>();
            episode = FindAnyObjectByType<LeonidEvacuee>();
        }

        private void Update()
        {
            target = null;
            cutoff = null;
            leonid = null;
            cane = null;
            vadim = null;
            inspection = null;
            if (player == null || viewCamera == null || !player.HasControl ||
                loop != null && loop.SuppressInteractionUI)
                return;

            Ray ray = viewCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            // Start at the camera itself so the distance includes its near clipping plane.
            ray.origin = viewCamera.transform.position;
            // Use the FIRST solid hit, including walls. The player uses Ignore Raycast layer.
            if (Physics.Raycast(ray, out RaycastHit hit, interactionDistance,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                target = hit.collider.GetComponentInParent<PrototypeDoor>();
                cutoff = hit.collider.GetComponentInParent<EmergencyCutoff>();
                leonid = hit.collider.GetComponentInParent<LeonidEvacuee>();
                cane = hit.collider.GetComponentInParent<CanePickup>();
                vadim = hit.collider.GetComponentInParent<VadimEvacuee>();
                inspection = hit.collider.GetComponentInParent<EquipmentInspection>();
            }

            if (player.InteractionPressed)
            {
                if (cutoff != null) cutoff.Interact();
                else if (leonid != null) leonid.Interact();
                else if (cane != null) cane.Interact();
                else if (vadim != null) vadim.Interact();
                else if (inspection != null) inspection.Interact();
                else if (target != null) target.Toggle();
            }
        }

        private void OnGUI()
        {
            if (player == null)
                return;
            if (loop != null && loop.SuppressInteractionUI) return;

            if (labelStyle == null)
            {
                labelStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 20,
                    wordWrap = true
                };
                labelStyle.normal.textColor = Color.white;
            }

            if (player.HasControl)
            {
                GUI.DrawTexture(new Rect(Screen.width * 0.5f - 2f, Screen.height * 0.5f - 2f, 4f, 4f), Texture2D.whiteTexture);
                if (cutoff != null)
                    DrawLabel(cutoff.Prompt, Screen.height * 0.5f + 24f);
                else if (leonid != null)
                    DrawLabel(leonid.Prompt, Screen.height * 0.5f + 24f);
                else if (cane != null)
                    DrawLabel(cane.Prompt, Screen.height * 0.5f + 24f);
                else if (vadim != null)
                    DrawLabel(vadim.Prompt, Screen.height * 0.5f + 24f);
                else if (inspection != null)
                    DrawLabel(inspection.Prompt, Screen.height * 0.5f + 24f);
                else if (target != null)
                    DrawLabel(target.Prompt, Screen.height * 0.5f + 24f);
            }
            else
            {
                DrawLabel("Щёлкните в игровом окне, чтобы управлять", Screen.height * 0.5f + 24f);
            }
            if (episode != null && episode.CarryingCane)
                DrawLabel("Вы несёте трость · поговорите с Леонидом, чтобы передать её", Screen.height - 110f);
        }

        private void DrawLabel(string text, float y)
        {
            float width = Mathf.Min(620f, Screen.width - 16f);
            Rect rect = new Rect((Screen.width - width) * 0.5f, y, width, 60f);
            Color previous = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.75f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = previous;
            GUI.Label(rect, text, labelStyle);
        }
    }
}
