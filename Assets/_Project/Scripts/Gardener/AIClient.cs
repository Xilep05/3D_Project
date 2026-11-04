using System;
using System.IO;
using UnityEngine;

namespace GardenWise
{
    // owner 5 - sends a question to the LLM API and gives back the answer
    // the key is read from StreamingAssets/ai_config.json (never committed, see ai_config.example.json)
    public class AIClient : MonoBehaviour
    {
        [TextArea(4, 8)]
        [SerializeField] private string systemPrompt =
            "You are Gaston, a friendly master gardener in an educational game. " +
            "Answer in 2 to 3 short sentences, in the player's language, for beginners. " +
            "Only talk about gardening, plants, seasons and saving water. " +
            "Use the garden state below to give concrete advice. Never give the full solution of the level.";

        [SerializeField] private FallbackTips fallbackTips;

        private AIConfig config;

        public bool HasKey
        {
            get { return config != null && !string.IsNullOrEmpty(config.apiKey); }
        }

        // note: on Quest (Android) File.ReadAllText cannot read StreamingAssets, use UnityWebRequest there
        private void Awake()
        {
            string path = Path.Combine(Application.streamingAssetsPath, "ai_config.json");
            if (File.Exists(path)) config = JsonUtility.FromJson<AIConfig>(File.ReadAllText(path));
        }

        public void Ask(string question, string gardenContext, Action<string> onAnswer)
        {
            GameEvents.RaiseGardenerAsked(question);

            if (!HasKey)
            {
                onAnswer(fallbackTips != null ? fallbackTips.GetTip() : "Keep the soil moist, not soaked!");
                return;
            }

            // TODO owner 5: StartCoroutine with UnityWebRequest (POST JSON to config.apiUrl)
            // body = systemPrompt + gardenContext + question, then onAnswer(text of the reply)
            onAnswer("(API not connected yet)");
        }
    }

    [Serializable]
    public class AIConfig
    {
        public string apiUrl;
        public string apiKey;
        public string model;
    }
}
