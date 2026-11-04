using System.Text;
using UnityEngine;

namespace GardenWise
{
    // owner 5 - turns the current garden into a short text that Gaston can read
    public class GardenContextBuilder : MonoBehaviour
    {
        [SerializeField] private GardenGrid garden;
        [SerializeField] private WeatherSystem weather;
        [SerializeField] private WaterManager water;

        public string Build()
        {
            StringBuilder text = new StringBuilder();
            GameManager game = GameManager.Instance;

            if (game != null && game.CurrentLevel != null)
            {
                text.AppendLine("Level: " + game.CurrentLevel.levelName + ", season: " + game.CurrentLevel.season);
                text.AppendLine("Day " + game.CurrentDay + " of " + game.CurrentLevel.numberOfDays);
            }

            if (weather != null) text.AppendLine("Weather today: " + weather.Today + ", tomorrow: " + weather.Tomorrow);
            if (water != null) text.AppendLine("Rain barrel: " + Mathf.RoundToInt(water.BarrelLitres) + " L");

            if (garden != null)
            {
                foreach (SoilTile tile in garden.Tiles)
                {
                    Plant plant = tile.CurrentPlant;
                    if (plant == null || plant.Data == null) continue;

                    text.AppendLine("- " + plant.Data.cropName + ": " + plant.Stage +
                                    ", health " + plant.Health + "/" + plant.Data.maxHealth +
                                    ", soil moisture " + Mathf.RoundToInt(tile.Moisture) +
                                    " (ideal " + plant.Data.idealMoistureMin + "-" + plant.Data.idealMoistureMax + ")" +
                                    (tile.HasMulch ? ", mulched" : ""));
                }
            }

            return text.ToString();
        }
    }
}
