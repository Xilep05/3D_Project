using UnityEngine;

namespace GardenWise
{
    // owner 3 - one square of soil: keeps its moisture and the plant growing on it
    public class SoilTile : MonoBehaviour
    {
        [SerializeField] private Plant plantPrefab;
        [SerializeField] private Transform plantPoint;
        [SerializeField] private float moisturePerLitre = 10f;

        public float Moisture { get; private set; } = 50f;
        public bool HasMulch { get; private set; }
        public Plant CurrentPlant { get; private set; }

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
        }

        public bool PlantCrop(CropData crop)
        {
            if (CurrentPlant != null || plantPrefab == null) return false;

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
    }
}
