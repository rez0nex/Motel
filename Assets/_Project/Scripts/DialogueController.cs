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
        private GUIStyle topicStyle;
        public bool IsOpen { get; private set; }
        public string Speaker => speaker;
        public string Topic { get; private set; }
        public int ChoiceCount => choices.Length;
        public UnityEngine.Object Participant { get; private set; }

        public void Configure(MotelLoopController controller) => loop = controller;

        public bool Show(string name, string text, params Choice[] options)
            => ShowFor(null, name, text, options);

        public bool ShowFor(UnityEngine.Object participant, string name, string text, params Choice[] options)
        {
            if (loop == null || loop.IsPaused || loop.Transitioning) return false;
            speaker = name;
            Topic = null;
            Participant = participant;
            line = text;
            choices = options ?? Array.Empty<Choice>();
            IsOpen = true;
            loop.RefreshPlayerControl();
            return true;
        }

        public bool ShowTopicFor(UnityEngine.Object participant, string name, string topic, string text, params Choice[] options)
        {
            if (!ShowFor(participant, name, text, options)) return false;
            Topic = topic;
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
            Participant = null;
            Topic = null;
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
                topicStyle = new GUIStyle(GUI.skin.label) { fontSize = 15 };
                topicStyle.normal.textColor = new Color(0.8f, 0.8f, 0.8f);
                heading.normal.textColor = body.normal.textColor = Color.white;
            }
            // A logical canvas keeps all choices visible even in a small Game View.
            Matrix4x4 previous = GUI.matrix;
            float scale = Mathf.Min(Screen.width / 960f, Screen.height / 600f);
            float width = Screen.width / scale, height = Screen.height / scale;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1));
            bool withTopic = !string.IsNullOrEmpty(Topic);
            float panelHeight = withTopic ? 223 + 54 * Mathf.Max(1, choices.Length) : 325;
            Rect panel = new Rect((width - 820) / 2, height - panelHeight - 20, 820, panelHeight);
            GUI.Box(panel, GUIContent.none);
            GUI.Label(new Rect(panel.x + 22, panel.y + 12, 776, 38), withTopic ? "Говорит: " + speaker : speaker, heading);
            if (withTopic) GUI.Label(new Rect(panel.x + 22, panel.y + 44, 776, 24), "Тема: " + Topic, topicStyle);
            GUI.Label(new Rect(panel.x + 22, panel.y + (withTopic ? 74 : 56), 776, withTopic ? 105 : 95), line, body);
            bool previousEnabled = GUI.enabled;
            GUI.enabled = !loop.IsPaused;
            for (int i = 0; i < choices.Length; i++)
            {
                if (GUI.Button(new Rect(panel.x + 22, panel.y + (withTopic ? 190 : 153) + i * (withTopic ? 54 : 48),
                        776, withTopic ? 48 : 42), choices[i].Text, button))
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
