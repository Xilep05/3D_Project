using System.Collections;
using UnityEngine;

namespace GardenWise
{
    // owner 2 - final view of a Plant (replaces PlantPlaceholderView from owner 3).
    // Shows the model of the current stage taken from CropData.stageModels (Seed, Sprout, Young, Mature, Ready),
    // or a simple shape when the stage has no model.
    // Visual only: it READS Plant and never changes it (data / visual separation, Course 4 - Modularity).
    // Adding a new crop = a new CropData with its models: no code and no new prefab.
    [RequireComponent(typeof(Plant))]
    public class PlantVisual : MonoBehaviour
    {
        [Header("Fallback shape (stage without model)")]
        [SerializeField] private Transform shape;
        [SerializeField] private Renderer shapeRenderer;

        [Header("Size")]
        [Tooltip("Width of the grown plant, so it fits on a 1.2 m tile")]
        [SerializeField] private float grownWidth = 0.9f;

        [Header("Feedback")]
        [Tooltip("Duration of the small 'pop' when the plant reaches a new stage")]
        [SerializeField] private float popDuration = 0.3f;
        [SerializeField] private Color deadTint = new Color(0.45f, 0.33f, 0.2f);
        [SerializeField] private float deadTiltAngle = 25f;

        private Plant plant;
        private GrowthStage shownStage;
        private bool shownHarvested;
        private bool initialized;

        private GameObject currentModel;
        private CropData scaledFor;
        private float modelScale = 1f;
        private float turn;

        private void Awake()
        {
            plant = GetComponent<Plant>();
            turn = Random.Range(0f, 360f); // each plant slightly different, looks more natural
        }

        private void Update()
        {
            // cheap check every frame: only refreshes when something changed
            if (initialized && plant.Stage == shownStage && plant.IsHarvested == shownHarvested) return;
            Refresh();
        }

        private void Refresh()
        {
            bool stageChanged = !initialized || plant.Stage != shownStage;

            shownStage = plant.Stage;
            shownHarvested = plant.IsHarvested;
            initialized = true;

            StopAllCoroutines();

            // harvested: nothing left on the tile
            if (shownHarvested)
            {
                ClearModel();
                SetShapeActive(false);
                return;
            }

            // dead: keep the last model, but brown and leaning
            if (shownStage == GrowthStage.Dead)
            {
                ShowDead();
                return;
            }

            ClearModel();
            GameObject prefab = GetStageModel(shownStage);
            if (prefab != null)
            {
                SpawnModel(prefab);
                SetShapeActive(false);
            }
            else
            {
                ShowShape(shownStage);
            }

            if (stageChanged) StartPop();
        }

        // ---------- models from CropData ----------

        private GameObject GetStageModel(GrowthStage stage)
        {
            if (stage == GrowthStage.Dead || plant.Data == null || plant.Data.stageModels == null) return null;

            int index = (int)stage; // Seed = 0 ... Ready = 4, same order as CropData.stageModels
            return index < plant.Data.stageModels.Length ? plant.Data.stageModels[index] : null;
        }

        private void SpawnModel(GameObject prefab)
        {
            currentModel = Instantiate(prefab, transform);
            currentModel.transform.localPosition = Vector3.zero;
            currentModel.transform.localRotation = Quaternion.Euler(0f, turn, 0f) * prefab.transform.rotation;
            currentModel.transform.localScale = prefab.transform.localScale * GetModelScale();
        }

        private void ClearModel()
        {
            if (currentModel != null) Destroy(currentModel);
            currentModel = null;
        }

        // one scale for every stage, computed from the biggest model,
        // so the plant really grows from stage to stage and fits on its tile
        private float GetModelScale()
        {
            if (scaledFor == plant.Data) return modelScale;

            scaledFor = plant.Data;
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

        // ---------- dead plant ----------

        private void ShowDead()
        {
            if (currentModel == null)
            {
                ShowShape(GrowthStage.Dead);
                return;
            }

            currentModel.transform.Rotate(Vector3.right, deadTiltAngle, Space.World);
            currentModel.transform.localScale *= 0.85f;

            foreach (Renderer r in currentModel.GetComponentsInChildren<Renderer>())
            {
                foreach (Material mat in r.materials)
                {
                    if (mat.HasProperty("_BaseColor"))
                        mat.SetColor("_BaseColor", Color.Lerp(mat.GetColor("_BaseColor"), deadTint, 0.8f));
                    else if (mat.HasProperty("_Color"))
                        mat.SetColor("_Color", Color.Lerp(mat.GetColor("_Color"), deadTint, 0.8f));
                }
            }
        }

        // ---------- fallback shape (no model for this stage) ----------

        private void ShowShape(GrowthStage stage)
        {
            if (shape == null) return;
            SetShapeActive(true);

            float height = GetHeight(stage);
            float width = stage == GrowthStage.Seed ? 0.12f : 0.15f + height * 0.5f;
            shape.localScale = new Vector3(width, height / 2f, width);
            shape.localPosition = new Vector3(0f, height / 2f, 0f);

            if (shapeRenderer != null) shapeRenderer.material.color = GetColor(stage);
        }

        private void SetShapeActive(bool active)
        {
            if (shape != null && shape.gameObject.activeSelf != active) shape.gameObject.SetActive(active);
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

        // ---------- growth "pop" ----------

        private void StartPop()
        {
            Transform target = currentModel != null ? currentModel.transform : shape;
            if (target == null || popDuration <= 0f) return;
            StartCoroutine(Pop(target, target.localScale));
        }

        private IEnumerator Pop(Transform target, Vector3 finalScale)
        {
            float t = 0f;
            while (t < popDuration)
            {
                if (target == null) yield break;

                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / popDuration);
                float s = Mathf.Lerp(0.6f, 1f, k) + Mathf.Sin(k * Mathf.PI) * 0.12f; // grows with a small bounce
                target.localScale = finalScale * s;
                yield return null;
            }
            if (target != null) target.localScale = finalScale;
        }
    }
}
