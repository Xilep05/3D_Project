using UnityEngine;

namespace GardenWise
{
    // owner 3 - one square of soil: keeps its moisture and the plant growing on it
    public class SoilTile : MonoBehaviour
    {
        [SerializeField] private Plant plantPrefab;
        [SerializeField] private Transform plantPoint;
        [SerializeField] private float moisturePerLitre = 10f;

        [Header("Visuals (optional)")]
        [Tooltip("Shown while the player aims at this tile")]
        [SerializeField] private GameObject highlight;
        [Tooltip("Shown once mulch has been added")]
        [SerializeField] private GameObject mulchVisual;

        public float Moisture { get; private set; } = 50f;
        public bool HasMulch { get; private set; }
        public Plant CurrentPlant { get; private set; }

        private void Awake()
        {
            SetHighlighted(false);
            if (mulchVisual != null) mulchVisual.SetActive(HasMulch);
        }

        // returns the litres that were wasted (soil already full)
        public float AddWater(float litres)
        {
            float before = Moisture;
            Moisture = Mathf.Clamp(Moisture + litres * moisturePerLitre, 0f, 100f);
            float usefulLitres = (Moisture - before) / moisturePerLitre;
            return litres - usefulLitres;
        }

        // used by the weather system at night (negative = drying)
        public void ChangeMoisture(float amount)
        {
            Moisture = Mathf.Clamp(Moisture + amount, 0f, 100f);
        }

        public void AddMulch()
        {
            HasMulch = true;
            if (mulchVisual != null) mulchVisual.SetActive(true);
        }

        public bool PlantCrop(CropData crop)
        {
            if (CurrentPlant != null || plantPrefab == null || crop == null) return false;

            Transform point = plantPoint != null ? plantPoint : transform;
            CurrentPlant = Instantiate(plantPrefab, point.position, Quaternion.identity, point);
            CurrentPlant.Setup(crop);
            GameEvents.RaisePlantPlanted(CurrentPlant);
            return true;
        }

        public void ClearPlant()
        {
            if (CurrentPlant != null) Destroy(CurrentPlant.gameObject);
            CurrentPlant = null;
        }

        // called by the Interactor when the player aims at this tile (or stops aiming at it)
        public void SetHighlighted(bool highlighted)
        {
            if (highlight != null) highlight.SetActive(highlighted);
        }
    }
}
