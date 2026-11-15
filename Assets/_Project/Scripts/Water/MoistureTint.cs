using UnityEngine;

namespace GardenWise
{
    // owner 4 - colours a soil tile from dry (light) to wet (dark) so the moisture can be seen
    [RequireComponent(typeof(SoilTile))]
    public class MoistureTint : MonoBehaviour
    {
        [SerializeField] private Renderer soilRenderer;
        [SerializeField] private Color dryColor = new Color(0.86f, 0.74f, 0.52f);
        [SerializeField] private Color wetColor = new Color(0.24f, 0.15f, 0.08f);

        private SoilTile tile;

        private void Awake()
        {
            tile = GetComponent<SoilTile>();
            if (soilRenderer == null) soilRenderer = GetComponent<Renderer>();
        }

        private void Update()
        {
            if (soilRenderer == null) return;
            soilRenderer.material.color = Color.Lerp(dryColor, wetColor, tile.Moisture / 100f);
        }
    }
}
