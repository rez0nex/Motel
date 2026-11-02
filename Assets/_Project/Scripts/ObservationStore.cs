using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace Motel
{
    // Knowledge outlives a scene. Independent flags keep facts idempotent.
    public sealed class ObservationStore
    {
        public const string CutoffFact = "Отключение оборудования вызвало взрыв";
        public const string LeonidFact = "Леонид из номера 4 добрался до безопасного места на парковке";
        public const string EquipmentFact = "Диагностика агрегата показывает критический перегрев: требуется эвакуация";
        public const string VadimFact = "Вадим из номера 2 самостоятельно добрался до безопасного места на парковке";
        [Serializable]
        private sealed class SaveData
        {
            public int version;
            public bool cutoffCausedExplosion;
            public bool leonidRescued;
            public bool equipmentOverheatInspected;
            public bool vadimRescued;
        }

        private readonly string path;
        private bool canWrite = true;
        public bool HasCutoffFact { get; private set; }
        public bool HasLeonidFact { get; private set; }
        public bool HasEquipmentFact { get; private set; }
        public bool HasVadimFact { get; private set; }
        public string[] Facts
        {
            get
            {
                var facts = new List<string>();
                if (HasCutoffFact) facts.Add(CutoffFact);
                if (HasEquipmentFact) facts.Add(EquipmentFact);
                if (HasLeonidFact) facts.Add(LeonidFact);
                if (HasVadimFact) facts.Add(VadimFact);
                return facts.ToArray();
            }
        }
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
                HasLeonidFact = data.leonidRescued;
                HasEquipmentFact = data.equipmentOverheatInspected;
                HasVadimFact = data.vadimRescued;
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

        public bool RememberLeonidRescue()
        {
            if (HasLeonidFact) return false;
            HasLeonidFact = true;
            Save();
            return true;
        }

        public bool RememberEquipmentOverheat()
        {
            if (HasEquipmentFact) return false;
            HasEquipmentFact = true;
            Save();
            return true;
        }

        public bool RememberVadimRescue()
        {
            if (HasVadimFact) return false;
            HasVadimFact = true;
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
                string json = JsonUtility.ToJson(new SaveData { version = 1, cutoffCausedExplosion = HasCutoffFact,
                    leonidRescued = HasLeonidFact, equipmentOverheatInspected = HasEquipmentFact,
                    vadimRescued = HasVadimFact }, true);
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
