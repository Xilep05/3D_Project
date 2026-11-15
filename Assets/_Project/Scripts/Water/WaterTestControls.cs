using System.Collections;
using TMPro;
using UnityEngine;

namespace GardenWise
{
    // owner 4 - only for the Test_Water scene: buttons, keys and a text panel to test the water and weather rules
    // keys: 1-3 select a tile, B barrel, T tap, M mulch, Space end the day
    public class WaterTestControls : MonoBehaviour
    {
        [SerializeField] private WaterManager water;
        [SerializeField] private WeatherSystem weather;
        [SerializeField] private ScoreManager score;
        [SerializeField] private SoilTile[] tiles;
        [SerializeField] private TMP_Text statsText;
        [SerializeField] private TMP_Text messageText;

        private int selectedTile;

        // the GameManager starts the test level in its Start, so the first day begins one frame later
        private IEnumerator Start()
        {
            yield return null;
            GameManager game = GameManager.Instance;
            if (game != null && game.State == GameState.LevelIntro) game.BeginFirstDay();
            Show("Select a tile, water it, then end the day.");
        }

        private void Update()
        {
            ReadKeys();
            RefreshStats();
        }

        public void SelectTile(int index)
        {
            selectedTile = Mathf.Clamp(index, 0, tiles.Length - 1);
            Show("Tile " + (selectedTile + 1) + " selected");
        }

        public void WaterFromBarrel()
        {
            Water(WaterSource.RainBarrel);
        }

        public void WaterFromTap()
        {
            Water(WaterSource.Tap);
        }

        public void AddMulch()
        {
            tiles[selectedTile].AddMulch();
            Show("Mulch on tile " + (selectedTile + 1) + ": it now dries half as fast");
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
            Show("New day: " + WeatherSystem.GetLabel(weather.Today));
        }

        private void Water(WaterSource source)
        {
            if (water.WaterTile(tiles[selectedTile], source))
            {
                string from = source == WaterSource.Tap ? "the tap" : "the barrel";
                Show("+" + water.LitresPerPour + " L on tile " + (selectedTile + 1) + " from " + from);
            }
            else if (source == WaterSource.Tap)
            {
                Show("The tap is not allowed in this level");
            }
            else
            {
                Show("The barrel is empty: wait for the rain or use the tap");
            }
        }

        private void ReadKeys()
        {
            if (Input.GetKeyDown(KeyCode.Alpha1)) SelectTile(0);
            if (Input.GetKeyDown(KeyCode.Alpha2)) SelectTile(1);
            if (Input.GetKeyDown(KeyCode.Alpha3)) SelectTile(2);
            if (Input.GetKeyDown(KeyCode.B)) WaterFromBarrel();
            if (Input.GetKeyDown(KeyCode.T)) WaterFromTap();
            if (Input.GetKeyDown(KeyCode.M)) AddMulch();
            if (Input.GetKeyDown(KeyCode.Space)) EndDay();
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

            text += "Weather: " + WeatherSystem.GetLabel(weather.Today) + "\n\n";
            text += "Barrel: " + Mathf.RoundToInt(water.BarrelLitres) + " / " + Mathf.RoundToInt(water.BarrelCapacity) + " L\n";
            text += "Rain collected: " + Mathf.RoundToInt(water.RainLitresCollected) + " L\n";
            text += "Tap used: " + water.TapLitresUsed.ToString("0.#") + " L (counts double)\n";

            if (score != null)
            {
                text += "Useful: " + score.UsefulLitres.ToString("0.#") + " L   Wasted: " + score.WastedLitres.ToString("0.#") + " L\n";
                text += "Water efficiency: " + Mathf.RoundToInt(score.WaterEfficiency) + " %\n";
            }

            text += "\n";
            for (int i = 0; i < tiles.Length; i++)
            {
                string line = "Tile " + (i + 1) + ": moisture " + Mathf.RoundToInt(tiles[i].Moisture);
                if (tiles[i].HasMulch) line += " (mulch)";
                if (i == selectedTile) line = "<b>> " + line + "</b>";
                text += line + "\n";
            }

            statsText.text = text;
        }

        private void Show(string message)
        {
            if (messageText != null) messageText.text = message;
        }
    }
}
