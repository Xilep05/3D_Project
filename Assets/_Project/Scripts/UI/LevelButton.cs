using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GardenWise
{
    // owner 6 - one button of the level select: name, stars, best scores and lock
    public class LevelButton : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private TMP_Text label;
        [SerializeField] private TMP_Text bestText;

        [Header("Gold stars image (Image Type = Filled)")]
        [SerializeField] private Image starsFill;

        public void Show(string title, int stars, string best, bool unlocked)
        {
            label.text = title;
            bestText.text = best;
            starsFill.fillAmount = stars / 3f;
            button.interactable = unlocked;
        }
    }
}
