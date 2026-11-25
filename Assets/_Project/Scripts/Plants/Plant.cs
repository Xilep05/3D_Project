using UnityEngine;

namespace GardenWise
{
    // owner 2 - a growing plant: stage, health and growth each night
    public class Plant : MonoBehaviour
    {
        public CropData Data { get; private set; }
        public GrowthStage Stage { get; private set; } = GrowthStage.Seed;
        public int Health { get; private set; }

        // true once the plant has been harvested: it can't be harvested (or grow) again
        public bool IsHarvested { get; private set; }

        private int daysGrown;

        public bool IsReady
        {
            get { return Stage == GrowthStage.Ready; }
        }

        public bool IsDead
        {
            get { return Stage == GrowthStage.Dead; }
        }

        public void Setup(CropData crop)
        {
            Data = crop;
            Health = crop.maxHealth;
            daysGrown = 0;
            Stage = GrowthStage.Seed;
            IsHarvested = false;
        }

        // called by the GameManager once per night
        public void GrowOneDay(float moisture)
        {
            if (Data == null || IsDead || IsReady || IsHarvested) return;

            bool goodWater = moisture >= Data.idealMoistureMin && moisture <= Data.idealMoistureMax;
            if (goodWater)
            {
                daysGrown++;
            }
            else
            {
                Health--;
                if (Health <= 0)
                {
                    Stage = GrowthStage.Dead;
                    GameEvents.RaisePlantDied(this);
                    return;
                }
            }

            UpdateStage();
        }

        // returns the quality between 0 and 1
        // FIX double harvest: before, IsReady stayed true after a harvest, so a second call
        // raised the event again and the harvest was counted twice.
        public float Harvest()
        {
            if (!IsReady || IsHarvested) return 0f;

            IsHarvested = true;
            float quality = (float)Health / Data.maxHealth;
            GameEvents.RaisePlantHarvested(this, quality);
            return quality;
        }

        private void UpdateStage()
        {
            float progress = (float)daysGrown / Data.daysToGrow;

            if (progress >= 1f) Stage = GrowthStage.Ready;
            else if (progress >= 0.66f) Stage = GrowthStage.Mature;
            else if (progress >= 0.33f) Stage = GrowthStage.Young;
            else if (progress > 0f) Stage = GrowthStage.Sprout;
        }
    }
}
