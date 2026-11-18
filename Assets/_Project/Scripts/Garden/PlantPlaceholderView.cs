using UnityEngine;

namespace GardenWise
{
    // owner 3 - TEST ONLY: shows the stage of a Plant with a simple shape in Test_Garden
    // it will be replaced by PlantVisual and the plant prefab of owner 2
    [RequireComponent(typeof(Plant))]
    public class PlantPlaceholderView : MonoBehaviour
    {
        [SerializeField] private Transform shape;
        [SerializeField] private Renderer shapeRenderer;

        private Plant plant;
        private GrowthStage shownStage;
        private bool shownOnce;

        private void Awake()
        {
            plant = GetComponent<Plant>();
        }

        private void Update()
        {
            if (shape == null) return;
            if (shownOnce && plant.Stage == shownStage) return;

            shownStage = plant.Stage;
            shownOnce = true;

            float height = GetHeight(shownStage);
            float width = shownStage == GrowthStage.Seed ? 0.12f : 0.15f + height * 0.5f;
            shape.localScale = new Vector3(width, height / 2f, width);
            shape.localPosition = new Vector3(0f, height / 2f, 0f);

            if (shapeRenderer != null) shapeRenderer.material.color = GetColor(shownStage);
        }

        private static float GetHeight(GrowthStage stage)
        {
            switch (stage)
            {
                case GrowthStage.Seed: return 0.05f;
                case GrowthStage.Sprout: return 0.15f;
                case GrowthStage.Young: return 0.3f;
                case GrowthStage.Mature: return 0.45f;
                case GrowthStage.Ready: return 0.55f;
                default: return 0.1f;
            }
        }

        private static Color GetColor(GrowthStage stage)
        {
            switch (stage)
            {
                case GrowthStage.Seed: return new Color(0.45f, 0.32f, 0.2f);
                case GrowthStage.Ready: return new Color(1f, 0.78f, 0.15f);
                case GrowthStage.Dead: return new Color(0.35f, 0.25f, 0.15f);
                default: return new Color(0.3f, 0.7f, 0.25f);
            }
        }
    }
}
