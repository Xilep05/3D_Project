using TMPro;
using UnityEngine;

namespace GardenWise
{
    // owner 3 - in the HUD: what the player is aiming at, and the result of the last action
    // ("Planted Radish", "The rain barrel is empty"...). Lives under the HUD panel of owner 6
    public class InteractionFeedback : MonoBehaviour
    {
        [SerializeField] private Interactor interactor;
        [SerializeField] private TMP_Text targetText;
        [SerializeField] private TMP_Text messageText;
        [SerializeField] private float messageDuration = 3f;

        private float messageTimeLeft;

        private void Awake()
        {
            if (interactor == null) interactor = FindFirstObjectByType<Interactor>();
        }

        private void OnEnable()
        {
            if (interactor != null) interactor.Message += Show;
            if (messageText != null) messageText.text = "";
        }

        private void OnDisable()
        {
            if (interactor != null) interactor.Message -= Show;
        }

        private void Update()
        {
            if (messageTimeLeft > 0f)
            {
                messageTimeLeft -= Time.deltaTime;
                if (messageTimeLeft <= 0f && messageText != null) messageText.text = "";
            }

            if (targetText != null) targetText.text = DescribeTarget();
        }

        private string DescribeTarget()
        {
            if (interactor == null) return "";
            if (interactor.TargetBell != null) return "Bell: click to end the day";

            SoilTile tile = interactor.TargetTile;
            if (tile == null) return "";

            string text = "Moisture " + Mathf.RoundToInt(tile.Moisture) + " / 100";
            if (tile.HasMulch) text += ", mulch";

            Plant plant = tile.CurrentPlant;
            if (plant != null && plant.Data != null)
            {
                text += "\n" + plant.Data.cropName + " (" + plant.Stage + "), likes " +
                        Mathf.RoundToInt(plant.Data.idealMoistureMin) + " to " + Mathf.RoundToInt(plant.Data.idealMoistureMax);
            }
            return text;
        }

        private void Show(string message)
        {
            if (messageText == null) return;
            messageText.text = message;
            messageTimeLeft = messageDuration;
        }
    }
}
