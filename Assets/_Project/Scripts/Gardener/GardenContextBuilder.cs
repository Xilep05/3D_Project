using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace GardenWise
{
    // owner 5 - describes the garden to Gaston: reads the real game systems,
    // or a test garden edited in the Inspector when those systems are not in the scene
    public class GardenContextBuilder : MonoBehaviour
    {
        [Header("Real game systems (found in the scene when left empty)")]
        [SerializeField] private GardenGrid garden;
        [SerializeField] private WeatherSystem weather;
        [SerializeField] private WaterManager water;

        [Header("Test garden (for a scene without the other systems)")]
        [SerializeField] private bool useTestGarden;
        [SerializeField] private GardenSnapshot testGarden = new GardenSnapshot
        {
            levelName = "Premières pousses",
            season = Season.Spring,
            day = 2,
            numberOfDays = 8,
            weatherToday = WeatherType.Sunny,
            weatherTomorrow = WeatherType.HeatWave,
            barrelLitres = 42f,
            tapAllowed = true,
            plants = new List<PlantSnapshot>
            {
                new PlantSnapshot
                {
                    cropName = "Radis", stage = GrowthStage.Sprout, health = 3, maxHealth = 3,
                    moisture = 55f, idealMoistureMin = 40f, idealMoistureMax = 70f
                },
                new PlantSnapshot
                {
                    cropName = "Laitue", stage = GrowthStage.Young, health = 2, maxHealth = 3,
                    moisture = 22f, idealMoistureMin = 50f, idealMoistureMax = 80f
                },
                new PlantSnapshot
                {
                    cropName = "Carotte", stage = GrowthStage.Seed, health = 3, maxHealth = 3,
                    moisture = 90f, idealMoistureMin = 40f, idealMoistureMax = 65f, mulched = true
                }
            }
        };

        // so the prefab works in the game with nothing to plug in
        private void Awake()
        {
            if (useTestGarden) return;

            if (garden == null) garden = FindAnyObjectByType<GardenGrid>();
            if (weather == null) weather = FindAnyObjectByType<WeatherSystem>();
            if (water == null) water = FindAnyObjectByType<WaterManager>();
        }

        // the text given to the model with every question
        public string Describe()
        {
            return Capture().Describe();
        }

        public GardenSnapshot Capture()
        {
            if (useTestGarden) return testGarden;

            GardenSnapshot snapshot = new GardenSnapshot();
            GameManager game = GameManager.Instance;

            if (game != null && game.CurrentLevel != null)
            {
                snapshot.levelName = game.CurrentLevel.levelName;
                snapshot.season = game.CurrentLevel.season;
                snapshot.day = game.CurrentDay;
                snapshot.numberOfDays = game.CurrentLevel.numberOfDays;
            }

            snapshot.weatherKnown = weather != null;
            if (weather != null)
            {
                snapshot.weatherToday = weather.Today;
                snapshot.weatherTomorrow = weather.Tomorrow;
            }

            snapshot.waterKnown = water != null;
            if (water != null)
            {
                snapshot.barrelLitres = water.BarrelLitres;
                snapshot.tapAllowed = water.TapAllowed;
            }

            if (garden == null) return snapshot;

            foreach (SoilTile tile in garden.Tiles)
            {
                Plant plant = tile.CurrentPlant;
                if (plant == null || plant.Data == null) continue;

                snapshot.plants.Add(new PlantSnapshot
                {
                    cropName = plant.Data.cropName,
                    stage = plant.Stage,
                    health = plant.Health,
                    maxHealth = plant.Data.maxHealth,
                    moisture = tile.Moisture,
                    idealMoistureMin = plant.Data.idealMoistureMin,
                    idealMoistureMax = plant.Data.idealMoistureMax,
                    mulched = tile.HasMulch
                });
            }

            return snapshot;
        }
    }

    // plain picture of the garden at one moment: the only thing Gaston knows about the game
    [Serializable]
    public class GardenSnapshot
    {
        [Header("Level")]
        public string levelName = "";
        public Season season = Season.Spring;
        public int day = 1;
        public int numberOfDays = 8;

        [Header("Weather")]
        public bool weatherKnown = true;
        public WeatherType weatherToday = WeatherType.Sunny;
        public WeatherType weatherTomorrow = WeatherType.Sunny;

        [Header("Water")]
        public bool waterKnown = true;
        public float barrelLitres = 60f;
        public bool tapAllowed = true;

        [Header("Plants")]
        public List<PlantSnapshot> plants = new List<PlantSnapshot>();

        // in French like the instructions, and with the conclusion already drawn for each plant
        // ("too dry, needs water"): a small model is bad at comparing numbers by itself
        public string Describe()
        {
            StringBuilder text = new StringBuilder();
            text.AppendLine("État du jardin :");

            if (!string.IsNullOrEmpty(levelName))
            {
                int daysLeft = Mathf.Max(numberOfDays - day, 0);
                text.AppendLine("Niveau « " + levelName + " », " + Name(season) + ", jour " + day + " sur " + numberOfDays +
                                " (il reste " + daysLeft + (daysLeft > 1 ? " jours" : " jour") + " après aujourd'hui).");
            }

            if (weatherKnown)
            {
                text.AppendLine("Météo : aujourd'hui " + Today(weatherToday) + ". Demain " + Tomorrow(weatherTomorrow) + ".");
            }

            if (waterKnown)
            {
                text.AppendLine("Eau : " + Mathf.RoundToInt(barrelLitres) + " L dans le récupérateur de pluie. " +
                                (tapAllowed ? "Robinet autorisé, à garder en dernier recours." : "Robinet interdit dans ce niveau."));
            }

            if (plants == null || plants.Count == 0)
            {
                text.AppendLine("Plantes : aucune plante pour l'instant.");
                return text.ToString();
            }

            text.AppendLine("Plantes (une ligne par plante du jardin) :");
            foreach (PlantSnapshot plant in plants)
            {
                string ideal = " (" + Mathf.RoundToInt(plant.moisture) + ", idéal " + Mathf.RoundToInt(plant.idealMoistureMin) +
                               " à " + Mathf.RoundToInt(plant.idealMoistureMax) + ")";

                // no "il" or "elle" here: the model would copy the gender, and a radish is not a "elle"
                if (plant.stage == GrowthStage.Dead)
                {
                    text.AppendLine("- " + plant.cropName + " : plante morte. À faire : rien, on ne peut plus la sauver.");
                    continue;
                }

                if (plant.stage == GrowthStage.Ready)
                {
                    text.AppendLine("- " + plant.cropName + " : plante mûre, santé " + plant.health + "/" + plant.maxHealth + ". À faire : récolter maintenant.");
                    continue;
                }

                string soil;
                string toDo;
                if (plant.moisture < plant.idealMoistureMin)
                {
                    soil = "terre trop sèche" + ideal;
                    toDo = "l'arroser";
                }
                else if (plant.moisture > plant.idealMoistureMax)
                {
                    soil = "terre trop mouillée" + ideal;
                    toDo = "ne plus l'arroser et attendre que la terre sèche";
                }
                else
                {
                    soil = "terre bien humide" + ideal;
                    toDo = "rien, tout va bien";
                }

                text.AppendLine("- " + plant.cropName + " : " + Name(plant.stage) + ", santé " + plant.health + "/" + plant.maxHealth +
                                ", " + soil + (plant.mulched ? ", déjà paillée" : "") + ". À faire : " + toDo + ".");
            }

            return text.ToString();
        }

        // whole clauses, ready to be reused by the model ("il fera canicule" is not French)
        private static string Today(WeatherType value)
        {
            switch (value)
            {
                case WeatherType.Rainy: return "il pleut";
                case WeatherType.HeatWave: return "c'est la canicule";
                case WeatherType.Frost: return "il gèle";
                default: return "il y a du soleil";
            }
        }

        // with what tomorrow's weather will do to the soil, so the model does not have to guess
        private static string Tomorrow(WeatherType value)
        {
            switch (value)
            {
                case WeatherType.Rainy: return "il pleuvra (la pluie arrosera toute seule et remplira le récupérateur)";
                case WeatherType.HeatWave: return "ce sera la canicule (la terre séchera très vite)";
                case WeatherType.Frost: return "il gèlera (il fera très froid)";
                default: return "il y aura du soleil";
            }
        }

        private static string Name(Season value)
        {
            switch (value)
            {
                case Season.Spring: return "printemps";
                case Season.Summer: return "été";
                case Season.Autumn: return "automne";
                default: return "hiver";
            }
        }

        private static string Name(GrowthStage value)
        {
            switch (value)
            {
                case GrowthStage.Seed: return "graine";
                case GrowthStage.Sprout: return "jeune pousse";
                case GrowthStage.Young: return "jeune plant";
                default: return "plant adulte";
            }
        }
    }

    [Serializable]
    public class PlantSnapshot
    {
        public string cropName = "Radish";
        public GrowthStage stage = GrowthStage.Seed;
        public int health = 3;
        public int maxHealth = 3;
        [Range(0, 100)] public float moisture = 50f;
        [Range(0, 100)] public float idealMoistureMin = 40f;
        [Range(0, 100)] public float idealMoistureMax = 70f;
        public bool mulched;
    }
}
