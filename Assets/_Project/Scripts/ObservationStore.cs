using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace Motel
{
    // Knowledge outlives a scene. A boolean also makes learning the sole fact idempotent.
    public sealed class ObservationStore
    {
        public const string CutoffFact = "Отключение оборудования вызвало взрыв";
        [Serializable]
        private sealed class SaveData
        {
            public int version;
            public bool cutoffCausedExplosion;
        }

        private readonly string path;
        private bool canWrite = true;
        public bool HasCutoffFact { get; private set; }
        public string Warning { get; private set; }

        public ObservationStore(string filePath)
        {
            path = filePath;
            try
            {
                if (!File.Exists(path)) return;
                SaveData data = JsonUtility.FromJson<SaveData>(File.ReadAllText(path, Encoding.UTF8));
                if (data == null || data.version != 1) throw new InvalidDataException("Unknown observation save version.");
                HasCutoffFact = data.cutoffCausedExplosion;
            }
            catch (Exception e) when (e is IOException || e is InvalidDataException || e is UnauthorizedAccessException || e is ArgumentException)
            {
                // Preserve an unreadable/future-version file, continue with session knowledge.
                canWrite = false;
                Warning = "Не удалось прочитать наблюдения. Исходный файл сохранён; новые знания доступны только в этой сессии.";
                Debug.LogWarning(Warning + " " + e.GetType().Name);
            }
        }

        public bool RememberCutoff()
        {
            if (HasCutoffFact) return false;
            HasCutoffFact = true;
            Save();
            return true;
        }

        private void Save()
        {
            if (!canWrite) return;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                string temporary = path + ".tmp";
                string json = JsonUtility.ToJson(new SaveData { version = 1, cutoffCausedExplosion = HasCutoffFact }, true);
                using (var file = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    byte[] bytes = new UTF8Encoding(false).GetBytes(json);
                    file.Write(bytes, 0, bytes.Length);
                    file.Flush(true);
                }
                if (File.Exists(path)) File.Replace(temporary, path, null);
                else File.Move(temporary, path);
                Warning = null;
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is NotSupportedException)
            {
                Warning = "Наблюдение сохранено в сессии, но запись на диск не удалась.";
                Debug.LogWarning(Warning + " " + e.GetType().Name);
            }
        }
    }
}
