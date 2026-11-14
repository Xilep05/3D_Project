using UnityEngine;

namespace GardenWise
{
    // owner 6 - harvest score, water efficiency and stars of the current level
    public class ScoreManager : MonoBehaviour
    {
        [SerializeField] private float tapPenalty = 2f;

        public int PlantsPlanted { get; private set; }
        public float HarvestQualityTotal { get; private set; }
        public float UsefulLitres { get; private set; }
        public float WastedLitres { get; private set; }
        public float TapLitres { get; private set; }
        public int QuestionsAsked { get; private set; }

        // 0 to 100
        public float HarvestScore
        {
            get { return PlantsPlanted == 0 ? 0f : Mathf.Min(HarvestQualityTotal / PlantsPlanted * 100f, 100f); }
        }

        // 0 to 100, tap litres count double
        public float WaterEfficiency
        {
            get
            {
                float counted = UsefulLitres + WastedLitres + TapLitres * (tapPenalty - 1f);
                return counted <= 0f ? 100f : UsefulLitres / counted * 100f;
            }
        }

        private void OnEnable()
        {
            GameEvents.GameStateChanged += HandleGameStateChanged;
            GameEvents.PlantPlanted += HandlePlanted;
            GameEvents.PlantHarvested += HandleHarvested;
            GameEvents.WaterUsed += HandleWaterUsed;
            GameEvents.GardenerAsked += HandleAsked;
        }

        private void OnDisable()
        {
            GameEvents.GameStateChanged -= HandleGameStateChanged;
            GameEvents.PlantPlanted -= HandlePlanted;
            GameEvents.PlantHarvested -= HandleHarvested;
            GameEvents.WaterUsed -= HandleWaterUsed;
            GameEvents.GardenerAsked -= HandleAsked;
        }

        public void ResetScore()
        {
            PlantsPlanted = 0;
            HarvestQualityTotal = 0f;
            UsefulLitres = 0f;
            WastedLitres = 0f;
            TapLitres = 0f;
            QuestionsAsked = 0;
        }

        public int GetStars(LevelData level)
        {
            if (HarvestScore >= level.harvestFor3Stars && WaterEfficiency >= level.waterFor3Stars) return 3;
            if (HarvestScore >= level.harvestFor2Stars && WaterEfficiency >= level.waterFor2Stars) return 2;
            return 1;
        }

        // every level starts with a fresh score
        private void HandleGameStateChanged(GameState state)
        {
            if (state == GameState.LevelIntro) ResetScore();
        }

        private void HandlePlanted(Plant plant) { PlantsPlanted++; }
        private void HandleHarvested(Plant plant, float quality) { HarvestQualityTotal += quality; }
        private void HandleAsked(string question) { QuestionsAsked++; }

        private void HandleWaterUsed(float litres, WaterSource source, bool wasted)
        {
            if (wasted) WastedLitres += litres;
            else UsefulLitres += litres;

            if (source == WaterSource.Tap) TapLitres += litres;
        }
    }
}
