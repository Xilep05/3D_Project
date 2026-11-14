using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GardenWise
{
    // owner 6 - end of level screen: shows the scores and the stars, then saves the best ones
    public class ResultPanel : MonoBehaviour
    {
        [SerializeField] private ScoreManager score;

        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text harvestText;
        [SerializeField] private TMP_Text waterText;
        [SerializeField] private TMP_Text questionsText;

        [Header("Gold stars image (Image Type = Filled)")]
        [SerializeField] private Image starsFill;

        // called by the MenuManager when the game state becomes Results
        public void Show(int levelIndex)
        {
            gameObject.SetActive(true);

            LevelData level = GameManager.Instance.CurrentLevel;
            int stars = score.GetStars(level);

            titleText.text = level.levelName;
            starsFill.fillAmount = stars / 3f;
            harvestText.text = "Harvest: " + Mathf.RoundToInt(score.HarvestScore) + " %";
            waterText.text = "Water efficiency: " + Mathf.RoundToInt(score.WaterEfficiency) + " %";
            questionsText.text = "Questions to Gaston: " + score.QuestionsAsked;

            SaveResult(levelIndex, stars);
        }

        // keeps the best stars and the best scores of each level in the JSON save file
        private void SaveResult(int levelIndex, int stars)
        {
            SaveData save = SaveSystem.Load();

            save.starsPerLevel[levelIndex] = Mathf.Max(save.starsPerLevel[levelIndex], stars);
            save.bestHarvestPerLevel[levelIndex] = Mathf.Max(save.bestHarvestPerLevel[levelIndex], score.HarvestScore);
            save.bestWaterPerLevel[levelIndex] = Mathf.Max(save.bestWaterPerLevel[levelIndex], score.WaterEfficiency);
            save.totalQuestionsAsked += score.QuestionsAsked;

            SaveSystem.Save(save);
        }
    }
}
