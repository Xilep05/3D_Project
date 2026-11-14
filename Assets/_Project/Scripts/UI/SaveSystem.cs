using System;
using System.IO;
using UnityEngine;

namespace GardenWise
{
    // owner 6 - saves the player progress in a JSON file (works on PC and on Quest)
    public static class SaveSystem
    {
        private const string FileName = "gardenwise_save.json";

        private static string FilePath
        {
            get { return Path.Combine(Application.persistentDataPath, FileName); }
        }

        public static SaveData Load()
        {
            if (!File.Exists(FilePath)) return new SaveData();

            // a broken file (game closed while saving...) must not block the game: start again from a new save
            try
            {
                SaveData data = JsonUtility.FromJson<SaveData>(File.ReadAllText(FilePath));
                if (data != null) return data;
            }
            catch (ArgumentException)
            {
                Debug.LogWarning("Save file is broken, a new one will be created");
            }
            return new SaveData();
        }

        public static void Save(SaveData data)
        {
            File.WriteAllText(FilePath, JsonUtility.ToJson(data, true));
        }

        public static void Delete()
        {
            if (File.Exists(FilePath)) File.Delete(FilePath);
        }
    }

    [Serializable]
    public class SaveData
    {
        public int[] starsPerLevel = new int[5];
        public float[] bestHarvestPerLevel = new float[5];
        public float[] bestWaterPerLevel = new float[5];
        public float totalLitresSaved;
        public int totalQuestionsAsked;
        public int sessionsPlayed;
    }
}
