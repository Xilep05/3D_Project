using System.Collections;
using TMPro;
using UnityEngine;

namespace GardenWise
{
    // owner 3 - only for the Test_Garden scene: starts the test level and shows what the player does on screen
    // Space also ends the day (same as clicking the bell)
    public class GardenTestControls : MonoBehaviour
    {
        [Header("Player")]
        [SerializeField] private ToolBelt toolBelt;
        [SerializeField] private Interactor interactor;

        [Header("Other systems (can stay empty)")]
        [SerializeField] private WaterManager water;
        [SerializeField] private WeatherSystem weather;
        [SerializeField] private ScoreManager score;

        [Header("Texts")]
        [SerializeField] private TMP_Text toolText;
        [SerializeField] private TMP_Text targetText;
        [SerializeField] private TMP_Text statsText;
        [SerializeField] private TMP_Text messageText;

        [SerializeField] private float messageDuration = 4f;

        private float messageTimeLeft;

        private void OnEnable()
        {
            if (interactor != null) interactor.Message += Show;
        }

        private void OnDisable()
        {
            if (interactor != null) interactor.Message -= Show;
        }

        // the GameManager starts the test level in its Start, so the first day begins one frame later
        private IEnumerator Start()
        {
            yield return null;
            GameManager game = GameManager.Instance;
            if (game != null && game.State == GameState.LevelIntro) game.BeginFirstDay();
            Show("Plant a seed (1), water it (2), ring the bell to end the day, harvest when it is fully grown (4)");
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Space)) EndDay();

            if (messageTimeLeft > 0f)
            {
                messageTimeLeft -= Time.deltaTime;
                if (messageTimeLeft <= 0f && messageText != null) messageText.text = "";
            }

            RefreshTool();
            RefreshTarget();
            RefreshStats();
        }

        public void EndDay()
        {
            GameManager game = GameManager.Instance;
            if (game == null) return;

            if (game.State != GameState.Playing)
            {
                Show("The level is finished: stop and start Play again to restart.");
                return;
            }

            game.EndDay();

            if (game.State == GameState.Results) Show("Level finished! Stop and start Play again to restart.");
            else if (weather != null) Show("Day " + game.CurrentDay + ": " + WeatherSystem.GetLabel(weather.Today));
        }

        private void RefreshTool()
        {
            if (toolText == null || toolBelt == null) return;
            toolText.text = "<b>" + ((int)toolBelt.CurrentTool + 1) + ". " + toolBelt.GetLabel() + "</b>";
        }

        private void RefreshTarget()
        {
            if (targetText == null || interactor == null) return;

            if (interactor.TargetBell != null)
            {
                targetText.text = "Bell: click to end the day";
                return;
            }

            SoilTile tile = interactor.TargetTile;
            if (tile == null)
            {
                targetText.text = "";
                return;
            }

            string text = "Soil moisture " + Mathf.RoundToInt(tile.Moisture) + " / 100";
            if (tile.HasMulch) text += " (mulch)";

            Plant plant = tile.CurrentPlant;
            if (plant != null && plant.Data != null)
            {
                text += "\n" + plant.Data.cropName + ": " + plant.Stage + ", health " + plant.Health + " / " + plant.Data.maxHealth;
                text += "\nLikes " + Mathf.RoundToInt(plant.Data.idealMoistureMin) + " to " + Mathf.RoundToInt(plant.Data.idealMoistureMax);
            }
            else
            {
                text += "\nEmpty";
            }

            targetText.text = text;
        }

        private void RefreshStats()
        {
            if (statsText == null) return;

            GameManager game = GameManager.Instance;
            string text = "";

            if (game != null && game.CurrentLevel != null)
            {
                text += "Day " + game.CurrentDay + " / " + game.CurrentLevel.numberOfDays + "   (" + game.State + ")\n";
            }

            if (weather != null)
            {
                text += "Today: " + WeatherSystem.GetLabel(weather.Today) + "   Tomorrow: " + WeatherSystem.GetLabel(weather.Tomorrow) + "\n";
            }

            if (water != null)
            {
                text += "Barrel: " + Mathf.RoundToInt(water.BarrelLitres) + " / " + Mathf.RoundToInt(water.BarrelCapacity) + " L";
                text += "   Tap used: " + water.TapLitresUsed.ToString("0.#") + " L\n";
            }

            if (score != null)
            {
                text += "Harvest score: " + Mathf.RoundToInt(score.HarvestScore) + " %   Water efficiency: " + Mathf.RoundToInt(score.WaterEfficiency) + " %\n";
            }

            text += "\n<size=80%>ZQSD/WASD walk, mouse look, 1-4 or wheel: tool\n";
            text += "Press 1 again: next seed, 2 again: barrel / tap\n";
            text += "Click: use the tool or ring the bell, Space: end the day, Esc: free the mouse</size>";

            statsText.text = text;
        }

        private void Show(string message)
        {
            if (messageText == null) return;
            messageText.text = message;
            messageTimeLeft = messageDuration;
        }
    }
}
