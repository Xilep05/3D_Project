using UnityEngine;

namespace GardenWise
{
    // owner 3 - small particle bursts on the tile: water drops when watering, earth when planting,
    // straw when mulching, golden sparkles and leaves when harvesting
    public class InteractionEffects : MonoBehaviour
    {
        [SerializeField] private Interactor interactor;
        [Tooltip("Soft round dot, alpha blended (Art/Environment/Mat_FX_Particle)")]
        [SerializeField] private Material particleMaterial;

        private ParticleSystem splash;
        private ParticleSystem dust;
        private ParticleSystem straw;
        private ParticleSystem sparkles;

        private void Awake()
        {
            if (interactor == null) interactor = GetComponent<Interactor>();

            splash = CreateSystem("Water Splash", new Color(0.55f, 0.8f, 1f, 0.9f), 0.06f, 2.2f, 1.6f, 0.7f);
            dust = CreateSystem("Earth Dust", new Color(0.45f, 0.32f, 0.2f, 0.8f), 0.12f, 1.2f, 0.4f, 0.8f);
            straw = CreateSystem("Straw", new Color(0.9f, 0.78f, 0.45f, 0.9f), 0.07f, 1.5f, 1f, 0.9f);
            sparkles = CreateSystem("Harvest Sparkles", new Color(1f, 0.85f, 0.3f, 1f), 0.09f, 1.8f, -0.25f, 1.2f);
        }

        private void OnEnable()
        {
            if (interactor == null) return;
            interactor.Planted += OnPlanted;
            interactor.Watered += OnWatered;
            interactor.Mulched += OnMulched;
            interactor.Harvested += OnHarvested;
        }

        private void OnDisable()
        {
            if (interactor == null) return;
            interactor.Planted -= OnPlanted;
            interactor.Watered -= OnWatered;
            interactor.Mulched -= OnMulched;
            interactor.Harvested -= OnHarvested;
        }

        private void OnPlanted(SoilTile tile) { Burst(dust, tile, 18, 0.25f); }
        private void OnWatered(SoilTile tile) { Burst(splash, tile, 40, 0.9f); }
        private void OnMulched(SoilTile tile) { Burst(straw, tile, 30, 0.6f); }
        private void OnHarvested(SoilTile tile) { Burst(sparkles, tile, 35, 0.2f); }

        // particles start a little above the soil, spread over the tile
        private void Burst(ParticleSystem system, SoilTile tile, int count, float startHeight)
        {
            if (system == null || tile == null) return;
            system.transform.position = tile.transform.position + Vector3.up * (0.2f + startHeight);
            system.Emit(count);
        }

        private ParticleSystem CreateSystem(string name, Color color, float size, float speed, float gravity, float lifetime)
        {
            GameObject systemObject = new GameObject(name);
            systemObject.transform.SetParent(transform, false);

            ParticleSystem system = systemObject.AddComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule main = system.main;
            main.playOnAwake = false;
            main.loop = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime * 0.6f, lifetime);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.5f, speed);
            main.startSize = new ParticleSystem.MinMaxCurve(size * 0.6f, size * 1.3f);
            main.startColor = color;
            main.gravityModifier = gravity;
            main.maxParticles = 200;

            ParticleSystem.EmissionModule emission = system.emission;
            emission.rateOverTime = 0f;

            // a wide cone pointing up, as big as a tile
            ParticleSystem.ShapeModule shape = system.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 35f;
            shape.radius = 0.45f;
            systemObject.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);

            // fade out at the end of their life
            ParticleSystem.ColorOverLifetimeModule fade = system.colorOverLifetime;
            fade.enabled = true;
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.6f), new GradientAlphaKey(0f, 1f) });
            fade.color = new ParticleSystem.MinMaxGradient(gradient);

            ParticleSystemRenderer particleRenderer = systemObject.GetComponent<ParticleSystemRenderer>();
            if (particleMaterial != null) particleRenderer.sharedMaterial = particleMaterial;
            return system;
        }
    }
}
