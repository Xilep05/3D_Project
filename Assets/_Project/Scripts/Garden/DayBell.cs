using UnityEngine;

namespace GardenWise
{
    // owner 3 - the bell of the garden: the player rings it to end the day, then the GameManager (owner 1) simulates the night
    public class DayBell : MonoBehaviour
    {
        [SerializeField] private Transform swingPart;
        [SerializeField] private AudioSource ringSound;
        [SerializeField] private float swingDuration = 0.8f;
        [SerializeField] private float swingAngle = 25f;

        private float swingTimeLeft;

        // returns false when no day is running (menu, results, or no GameManager in the scene)
        public bool Ring()
        {
            GameManager game = GameManager.Instance;
            if (game == null || !game.IsPlaying) return false;

            swingTimeLeft = swingDuration;
            if (ringSound != null) ringSound.Play();

            game.EndDay();
            return true;
        }

        private void Update()
        {
            if (swingPart == null || swingTimeLeft <= 0f) return;

            swingTimeLeft -= Time.deltaTime;
            if (swingTimeLeft <= 0f)
            {
                swingPart.localRotation = Quaternion.identity;
                return;
            }

            // a swing that slows down
            float fade = swingTimeLeft / swingDuration;
            float angle = Mathf.Sin(swingTimeLeft * 25f) * swingAngle * fade;
            swingPart.localRotation = Quaternion.Euler(0f, 0f, angle);
        }
    }
}
