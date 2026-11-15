using UnityEngine;

namespace GardenWise
{
    // owner 4 - 3D gauge of the rain barrel: the blue part grows with the litres left
    public class RainBarrel : MonoBehaviour
    {
        [SerializeField] private WaterManager water;

        [Header("Two cylinders with the same radius, stacked")]
        [SerializeField] private Transform waterPart;
        [SerializeField] private Transform emptyPart;
        [SerializeField] private float barrelHeight = 1.2f;

        private void Update()
        {
            if (water == null) return;
            ShowLevel(water.BarrelRatio);
        }

        private void ShowLevel(float ratio)
        {
            float waterHeight = Mathf.Max(0.01f, ratio * barrelHeight);
            float emptyHeight = Mathf.Max(0.01f, barrelHeight - waterHeight);

            // a Unity cylinder is 2 units high at scale 1, with its pivot in the middle
            SetCylinder(waterPart, waterHeight, waterHeight / 2f);
            SetCylinder(emptyPart, emptyHeight, waterHeight + emptyHeight / 2f);
        }

        private void SetCylinder(Transform part, float height, float centerY)
        {
            if (part == null) return;

            Vector3 scale = part.localScale;
            scale.y = height / 2f;
            part.localScale = scale;

            Vector3 position = part.localPosition;
            position.y = centerY;
            part.localPosition = position;
        }
    }
}
