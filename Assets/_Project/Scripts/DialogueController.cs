using System;
using UnityEngine;

namespace Motel
{
    // Small reusable text dialogue; choices own their consequences, not the window.
    [DisallowMultipleComponent]
    public sealed class DialogueController : MonoBehaviour
    {
        public sealed class Choice
        {
            public readonly string Text;
            public readonly Action Select;
            public Choice(string text, Action select) { Text = text; Select = select; }
        }

        [SerializeField] private MotelLoopController loop;
        private Choice[] choices = Array.Empty<Choice>();
        private string speaker, line;
        private GUIStyle heading, body, button;
        public bool IsOpen { get; private set; }
        public string Speaker => speaker;
        public int ChoiceCount => choices.Length;

        public void Configure(MotelLoopController controller) => loop = controller;

        public bool Show(string name, string text, params Choice[] options)
        {
            if (loop == null || loop.IsPaused || loop.Transitioning) return false;
            speaker = name;
            line = text;
            choices = options ?? Array.Empty<Choice>();
            IsOpen = true;
            loop.RefreshPlayerControl();
            return true;
        }

        public void Choose(int index)
        {
            if (!IsOpen || loop.IsPaused || loop.Transitioning || index < 0 || index >= choices.Length) return;
            // Consume before calling: a second GUI event cannot execute an old choice again.
            Action action = choices[index].Select;
            choices = Array.Empty<Choice>();
            action?.Invoke();
        }

        public void Close()
        {
            IsOpen = false;
            choices = Array.Empty<Choice>();
            if (loop != null) loop.RefreshPlayerControl();
        }

        private void OnGUI()
        {
            if (!IsOpen || loop == null || loop.Transitioning) return;
            if (heading == null)
            {
                heading = new GUIStyle(GUI.skin.label) { fontSize = 24, fontStyle = FontStyle.Bold };
                body = new GUIStyle(GUI.skin.label) { fontSize = 20, wordWrap = true };
                button = new GUIStyle(GUI.skin.button) { fontSize = 18, wordWrap = true };
                heading.normal.textColor = body.normal.textColor = Color.white;
            }
            // A logical canvas keeps all choices visible even in a small Game View.
            Matrix4x4 previous = GUI.matrix;
            float scale = Mathf.Min(Screen.width / 960f, Screen.height / 600f);
            float width = Screen.width / scale, height = Screen.height / scale;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1));
            Rect panel = new Rect((width - 820) / 2, height - 345, 820, 325);
            GUI.Box(panel, GUIContent.none);
            GUI.Label(new Rect(panel.x + 22, panel.y + 12, 776, 38), speaker, heading);
            GUI.Label(new Rect(panel.x + 22, panel.y + 56, 776, 95), line, body);
            bool previousEnabled = GUI.enabled;
            GUI.enabled = !loop.IsPaused;
            for (int i = 0; i < choices.Length; i++)
            {
                if (GUI.Button(new Rect(panel.x + 22, panel.y + 153 + i * 48, 776, 42), choices[i].Text, button))
                {
                    Choose(i);
                    break;
                }
            }
            GUI.enabled = previousEnabled;
            GUI.Label(new Rect(panel.x + 22, panel.yMax - 28, 776, 26),
                loop.IsPaused ? "Пауза · щёлкните в игровом окне, чтобы продолжить" : "Время идёт · Escape — закрыть и поставить на паузу", body);
            GUI.matrix = previous;
        }
    }
}
