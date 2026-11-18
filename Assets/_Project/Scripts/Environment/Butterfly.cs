using UnityEngine;

namespace GardenWise
{
    // decor - a butterfly that flutters from one point to another around where it was placed
    public class Butterfly : MonoBehaviour
    {
        [SerializeField] private Transform leftWing;
        [SerializeField] private Transform rightWing;
        [SerializeField] private float flapSpeed = 16f;
        [SerializeField] private float speed = 0.8f;
        [SerializeField] private float wanderRadius = 3f;
        [SerializeField] private Vector2 heightRange = new Vector2(0.4f, 1.6f);

        private Vector3 home;
        private Vector3 target;
        private Vector3 velocity;
        private float phase;

        private void Start()
        {
            home = transform.position;
            phase = Random.Range(0f, 10f);
            PickTarget();
        }

        private void Update()
        {
            // wings: quick flaps, sometimes a short glide
            float time = Time.time + phase;
            float glide = Mathf.PerlinNoise(time * 0.7f, phase) > 0.75f ? 0.25f : 1f;
            float angle = Mathf.Sin(time * flapSpeed) * 60f * glide + 10f;
            if (leftWing != null) leftWing.localRotation = Quaternion.Euler(0f, 0f, angle);
            if (rightWing != null) rightWing.localRotation = Quaternion.Euler(0f, 0f, -angle);

            // erratic flight towards a target, a new target once it is reached
            Vector3 toTarget = target - transform.position;
            if (toTarget.magnitude < 0.2f) PickTarget();

            Vector3 wobble = new Vector3(Mathf.Sin(time * 3.1f), Mathf.Sin(time * 4.3f) * 0.6f, Mathf.Cos(time * 2.7f)) * 0.4f;
            Vector3 wanted = toTarget.normalized * speed + wobble;
            velocity = Vector3.Lerp(velocity, wanted, Time.deltaTime * 2f);
            transform.position += velocity * Time.deltaTime;

            Vector3 flat = new Vector3(velocity.x, 0f, velocity.z);
            if (flat.sqrMagnitude > 0.001f)
            {
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(flat), Time.deltaTime * 4f);
            }
        }

        private void PickTarget()
        {
            Vector2 circle = Random.insideUnitCircle * wanderRadius;
            target = home + new Vector3(circle.x, 0f, circle.y);
            target.y = Random.Range(heightRange.x, heightRange.y);
        }
    }
}
