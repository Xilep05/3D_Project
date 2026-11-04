using System.Collections.Generic;
using UnityEngine;

namespace GardenWise
{
    // owner 3 - finds every SoilTile placed under this object
    public class GardenGrid : MonoBehaviour
    {
        public List<SoilTile> Tiles { get; } = new List<SoilTile>();

        private void Awake()
        {
            Tiles.AddRange(GetComponentsInChildren<SoilTile>());
        }
    }
}
