using UnityEngine;

namespace GardenWise
{
    // owner 3 - TEMPORARY view of a Plant until PlantVisual (owner 2) is ready
    // shows the model of the current stage from CropData.stageModels (Seed, Sprout, Young, Mature, Ready),
    // or a simple shape when the stage has no model (seed, dead plant, crop without models)
    [RequireComponent(typeof(Plant))]
    public class PlantPlaceholderView : MonoBehaviour
    {
        [SerializeField] private Transform shape;
        [SerializeField] private Renderer shapeRenderer;

        [Tooltip("Width of the grown plant, so it fits on a 1.2 m tile")]
        [SerializeField] private float grownWidth = 0.9f;

        private Plant plant;
        private GrowthStage shownStage;
        private bool shownOnce;
        private GameObject currentModel;
        private float modelScale = -1f;
        private float turn;

        private void Awake()
        {
            plant = GetComponent<Plant>();
            turn = Random.Range(0f, 360f);
        }

        private void Update()
        {
            if (shownOnce && plant.Stage == shownStage) return;

            shownStage = plant.Stage;
            shownOnce = true;

            if (currentModel != null) Destroy(currentModel);

            GameObject model = GetStageModel(shownStage);
            if (model != null)
            {
                currentModel = Instantiate(model, transform);
                currentModel.transform.localPosition = Vector3.zero;
                currentModel.transform.localRotation = Quaternion.Euler(0f, turn, 0f) * model.transform.rotation;
                currentModel.transform.localScale = model.transform.localScale * GetModelScale();
                if (shape != null) shape.gameObject.SetActive(false);
                return;
            }

            ShowShape(shownStage);
        }

        private GameObject GetStageModel(GrowthStage stage)
        {
            if (stage == GrowthStage.Dead || plant.Data == null || plant.Data.stageModels == null) return null;

            int index = (int)stage;
            return index < plant.Data.stageModels.Length ? plant.Data.stageModels[index] : null;
        }

        // one scale for every stage, from the biggest one, so the plant really grows from stage to stage
        private float GetModelScale()
        {
            if (modelScale > 0f) return modelScale;
            modelScale = 1f;

            GameObject[] models = plant.Data.stageModels;
            for (int i = models.Length - 1; i >= 0; i--)
            {
                if (models[i] == null) continue;

                GameObject probe = Instantiate(models[i]);
                Renderer[] renderers = probe.GetComponentsInChildren<Renderer>();
                if (renderers.Length > 0)
                {
                    Bounds bounds = renderers[0].bounds;
                    for (int r = 1; r < renderers.Length; r++) bounds.Encapsulate(renderers[r].bounds);
                    float width = Mathf.Max(bounds.size.x, bounds.size.z);
                    if (width > 0.001f) modelScale = grownWidth / width;
                }
                Destroy(probe);
                break;
            }
            return modelScale;
        }

        private void ShowShape(GrowthStage stage)
        {
            if (shape == null) return;
            shape.gameObject.SetActive(true);

            float height = GetHeight(stage);
            float width = stage == GrowthStage.Seed ? 0.12f : 0.15f + height * 0.5f;
            shape.localScale = new Vector3(width, height / 2f, width);
            shape.localPosition = new Vector3(0f, height / 2f, 0f);

            if (shapeRenderer != null) shapeRenderer.material.color = GetColor(stage);
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
