using System.Collections.Generic;
using UnityEngine;

namespace GardenWise
{
    // owner 3 - finds every SoilTile placed under this object, and gives them fresh soil at the start of each level
    public class GardenGrid : MonoBehaviour
    {
        [SerializeField] private float startMoisture = 50f;

        public List<SoilTile> Tiles { get; } = new List<SoilTile>();

        private void Awake()
        {
            Tiles.AddRange(GetComponentsInChildren<SoilTile>());
        }

        private void OnEnable()
        {
            GameEvents.GameStateChanged += HandleGameStateChanged;
        }

        private void OnDisable()
        {
            GameEvents.GameStateChanged -= HandleGameStateChanged;
        }

        // without this, the plants of the previous level would still be there when the next one starts
        private void HandleGameStateChanged(GameState state)
        {
            if (state != GameState.LevelIntro) return;

            foreach (SoilTile tile in Tiles)
            {
                tile.ResetTile(startMoisture);
            }
        }
    }
}
