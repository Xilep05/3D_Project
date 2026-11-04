using UnityEngine;

namespace GardenWise
{
    // owner 5 - pre-written tips used when there is no internet or no API key
    public class FallbackTips : MonoBehaviour
    {
        [TextArea]
        [SerializeField] private string[] tips =
        {
            "Water in the morning or the evening, not at midday.",
            "Use the rain barrel first, tap water should be the last option.",
            "Mulch keeps the soil wet longer, so you need less water.",
            "Too much water is as bad as too little: the roots can rot."
        };

        private int nextTip;

        public string GetTip()
        {
            if (tips.Length == 0) return "";
            string tip = tips[nextTip];
            nextTip = (nextTip + 1) % tips.Length;
            return tip;
        }
    }
}
