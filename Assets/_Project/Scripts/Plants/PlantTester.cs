using UnityEngine;

namespace GardenWise
{
    // owner 2 - TEST CHAMBER ONLY (scene Test_Plants). Not used in the real game.
    // Lets you simulate nights and soil moisture with the keyboard to watch plants grow.
    //   N        : next night (every plant grows one day with the current moisture)
    //   Up/Down  : change soil moisture
    //   H        : harvest every plant that is ready
    //   R        : reset all plants to seed
    public class PlantTester : MonoBehaviour
    {
        [Tooltip("Plants placed in the scene")]
        [SerializeField] private Plant[] plants;

        [Tooltip("Crop data for each plant, SAME ORDER as the plants list")]
        [SerializeField] private CropData[] crops;

        [SerializeField] private float moisture = 50f;
        [SerializeField] private float moistureStep = 10f;

        private int night;
        private string lastMessage = "";

        private void Start()
        {
            ResetAll();
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.N)) NextNight();
            if (Input.GetKeyDown(KeyCode.UpArrow)) moisture += moistureStep;
            if (Input.GetKeyDown(KeyCode.DownArrow)) moisture -= moistureStep;
            if (Input.GetKeyDown(KeyCode.H)) HarvestAll();
            if (Input.GetKeyDown(KeyCode.R)) ResetAll();

            moisture = Mathf.Clamp(moisture, 0f, 100f);
        }

        private void NextNight()
        {
            night++;
            foreach (Plant plant in plants)
            {
                if (plant != null) plant.GrowOneDay(moisture);
            }
            lastMessage = "Nuit " + night + " passée";
        }

        private void HarvestAll()
        {
            lastMessage = "";
            foreach (Plant plant in plants)
            {
                if (plant == null) continue;
                float quality = plant.Harvest();
                if (quality > 0f)
                {
                    lastMessage += plant.Data.name + " récolté (qualité " + quality.ToString("0.00") + ")  ";
                }
            }
            if (lastMessage == "") lastMessage = "Rien à récolter";
        }

        private void ResetAll()
        {
            night = 0;
            for (int i = 0; i < plants.Length; i++)
            {
                if (plants[i] != null && i < crops.Length && crops[i] != null)
                {
                    plants[i].Setup(crops[i]);
                }
            }
            lastMessage = "Plantes remises à zéro";
        }

        private void OnGUI()
        {
            GUILayout.BeginArea(new Rect(10, 10, 460, 260), GUI.skin.box);
            GUILayout.Label("TEST PLANTES  -  Nuit " + night + "  |  Humidité du sol : " + moisture);
            GUILayout.Label("N : nuit suivante   Haut/Bas : humidité   H : récolter   R : reset");
            GUILayout.Space(6);

            foreach (Plant plant in plants)
            {
                if (plant == null || plant.Data == null) continue;
                GUILayout.Label(plant.Data.name
                    + " : " + plant.Stage
                    + "  |  PV " + plant.Health + "/" + plant.Data.maxHealth
                    + "  |  humidité idéale " + plant.Data.idealMoistureMin + "-" + plant.Data.idealMoistureMax
                    + (plant.IsHarvested ? "  |  RÉCOLTÉE" : ""));
            }

            GUILayout.Space(6);
            GUILayout.Label(lastMessage);
            GUILayout.EndArea();
        }
    }
}
