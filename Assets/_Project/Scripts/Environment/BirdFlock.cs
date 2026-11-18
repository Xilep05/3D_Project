using UnityEngine;

namespace GardenWise
{
    // decor - a few birds circling high above the garden, flapping then gliding
    public class BirdFlock : MonoBehaviour
    {
        [SerializeField] private Transform[] birds = new Transform[0];
        [SerializeField] private float radius = 28f;
        [SerializeField] private float height = 20f;
        [Tooltip("Metres per second; negative turns the other way")]
        [SerializeField] private float speed = 6f;

        private float angle;

        private void Update()
        {
            angle += speed / radius * Time.deltaTime;
            float direction = Mathf.Sign(speed);

            for (int i = 0; i < birds.Length; i++)
            {
                Transform bird = birds[i];
                if (bird == null) continue;

                // each bird a little behind and beside the previous one
                float own = angle - direction * i * 0.06f;
                float offset = (i % 2 == 0 ? 1f : -1f) * i * 0.8f;
                Vector3 around = new Vector3(Mathf.Cos(own), 0f, Mathf.Sin(own));
                float bob = Mathf.Sin(Time.time * 0.8f + i) * 0.6f;
                bird.position = transform.position + around * (radius + offset) + Vector3.up * (height + bob);

                Vector3 forward = new Vector3(-Mathf.Sin(own), 0f, Mathf.Cos(own)) * direction;
                bird.rotation = Quaternion.LookRotation(forward) * Quaternion.Euler(0f, 0f, -direction * 12f);

                // flap for a while, then glide with the wings open
                float cycle = Mathf.Repeat(Time.time * 0.25f + i * 0.37f, 1f);
                float wing = cycle < 0.55f ? Mathf.Sin(Time.time * 9f + i) * 35f : 5f;
                if (bird.childCount >= 3)
                {
                    bird.GetChild(1).localRotation = Quaternion.Euler(0f, 0f, wing);
                    bird.GetChild(2).localRotation = Quaternion.Euler(0f, 0f, -wing);
                }
            }
        }
    }
}
