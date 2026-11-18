using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;
using Random = System.Random;

namespace GardenWise
{
    // shared look of every scene (integration + owner 3): generated textures and matt materials,
    // warm sunlight, sky and fog, and the decor around the garden (fence, path, shed, trees, flowers, hills)
    // Used by "Build Test_Garden Scene" and "Build Main Scene". Everything is generated, no imported file needed.
    public static class EnvironmentArt
    {
        private const string ArtFolder = "Assets/_Project/Art/Environment";
        private const string PrefabFolder = "Assets/_Project/Prefabs/Environment";
        private const int TextureSize = 256;

        // garden layout shared with the scene builders (garden centre at z = 4)
        private const float FenceHalfWidth = 7f;
        private const float FenceFront = -2f;
        private const float FenceBack = 10.5f;
        private const float GateHalfWidth = 1.3f;

        [MenuItem("GardenWise/Regenerate Environment Textures")]
        public static void RegenerateTextures()
        {
            foreach (string name in new[] { "Tex_Grass", "Tex_Soil", "Tex_Gravel", "Tex_Wood" })
            {
                AssetDatabase.DeleteAsset(ArtFolder + "/" + name + ".png");
            }
            GroundMaterial();
            SoilMaterial();
            GravelMaterial("Mat_Env_GravelPlot", new Vector2(4f, 3f));
            WoodMaterial();
            Debug.Log("Environment textures regenerated. Rebuild the scenes to see them.");
        }

        // ================= materials =================

        public static Material GroundMaterial()
        {
            return Textured("Mat_Env_Grass", "Tex_Grass", GrassPixel, Color.white, new Vector2(70f, 70f), 0.05f);
        }

        // light and neutral: the MoistureTint of owner 4 gives the soil its colour (dry = light, wet = dark)
        public static Material SoilMaterial()
        {
            return Textured("Mat_Env_Soil", "Tex_Soil", SoilPixel, Color.white, Vector2.one, 0.05f);
        }

        public static Material WoodMaterial()
        {
            return Textured("Mat_Env_Wood", "Tex_Wood", WoodPixel, Color.white, Vector2.one, 0.1f);
        }

        public static Material DarkWoodMaterial()
        {
            return Textured("Mat_Env_DarkWood", "Tex_Wood", WoodPixel, new Color(0.62f, 0.55f, 0.5f), Vector2.one, 0.1f);
        }

        public static Material GravelMaterial(string name, Vector2 tiling)
        {
            return Textured(name, "Tex_Gravel", GravelPixel, Color.white, tiling, 0.08f);
        }

        public static Material HighlightMaterial()
        {
            return Plain("Mat_Env_Highlight", new Color(1f, 0.82f, 0.2f), 0.2f);
        }

        public static Material Plain(string name, Color color, float smoothness)
        {
            string path = ArtFolder + "/" + name + ".mat";
            EnsureFolder(ArtFolder);

            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Standard"));
                AssetDatabase.CreateAsset(material, path);
            }

            material.color = color;
            material.SetFloat("_Glossiness", smoothness);
            material.SetFloat("_Metallic", 0f);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material Textured(string name, string textureName, Func<int, int, Color> pixel, Color tint, Vector2 tiling, float smoothness)
        {
            Material material = Plain(name, tint, smoothness);
            material.mainTexture = GetTexture(textureName, pixel);
            material.mainTextureScale = tiling;
            EditorUtility.SetDirty(material);
            return material;
        }

        // ================= light, sky and fog =================

        public static void ApplyLighting()
        {
            Light sun = null;
            foreach (Light light in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                if (light.type == LightType.Directional) sun = light;
            }
            if (sun == null)
            {
                sun = new GameObject("Directional Light").AddComponent<Light>();
                sun.type = LightType.Directional;
            }

            // warm late-afternoon sun, soft shadows
            sun.name = "Sun";
            sun.transform.rotation = Quaternion.Euler(42f, -35f, 0f);
            sun.color = new Color(1f, 0.93f, 0.8f);
            sun.intensity = 1.15f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.75f;

            RenderSettings.sun = sun;
            RenderSettings.skybox = SkyMaterial();

            // three ambient colours: blue sky above, green-grey horizon, brown ground below
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.62f, 0.74f, 0.9f);
            RenderSettings.ambientEquatorColor = new Color(0.62f, 0.66f, 0.56f);
            RenderSettings.ambientGroundColor = new Color(0.33f, 0.29f, 0.24f);

            // light haze: gives depth and hides the edge of the world
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(0.76f, 0.84f, 0.9f);
            RenderSettings.fogDensity = 0.012f;
        }

        private static Material SkyMaterial()
        {
            string path = ArtFolder + "/Mat_Env_Sky.mat";
            EnsureFolder(ArtFolder);

            Material sky = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (sky == null)
            {
                sky = new Material(Shader.Find("Skybox/Procedural"));
                AssetDatabase.CreateAsset(sky, path);
            }

            sky.SetFloat("_SunSize", 0.04f);
            sky.SetFloat("_AtmosphereThickness", 0.85f);
            sky.SetColor("_SkyTint", new Color(0.55f, 0.66f, 0.86f));
            sky.SetColor("_GroundColor", new Color(0.45f, 0.5f, 0.4f));
            sky.SetFloat("_Exposure", 1.2f);
            EditorUtility.SetDirty(sky);
            return sky;
        }

        // ================= decor =================

        // builds the decor, saves it as Prefabs/Environment/Environment.prefab and returns the scene instance
        public static GameObject CreateEnvironment()
        {
            GameObject root = new GameObject("Environment");
            Random random = new Random(7);

            CreateGroundAndPaths(root.transform);
            CreateFence(root.transform);
            CreateShed(root.transform, new Vector3(-5f, 0f, 8.4f));
            CreateFlowers(root.transform, random);
            CreateTrees(root.transform, random);
            CreateBushes(root.transform, random);
            CreateRocks(root.transform, random);
            CreateHills(root.transform, random);

            // static batching: hundreds of small pieces drawn in a few calls (important later on the Quest)
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                GameObjectUtility.SetStaticEditorFlags(child.gameObject, StaticEditorFlags.BatchingStatic);
            }

            EnsureFolder(PrefabFolder);
            PrefabUtility.SaveAsPrefabAssetAndConnect(root, PrefabFolder + "/Environment.prefab", InteractionMode.AutomatedAction);
            return root;
        }

        private static void CreateGroundAndPaths(Transform parent)
        {
            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.SetParent(parent, false);
            ground.transform.localScale = new Vector3(14f, 1f, 14f);
            ground.GetComponent<Renderer>().sharedMaterial = GroundMaterial();

            // gravel under the garden, the barrel and the bell, and a path out of the gate
            Part(PrimitiveType.Cube, "Gravel Plot", parent, new Vector3(0f, 0.011f, 4f), new Vector3(9f, 0.02f, 6.8f), GravelMaterial("Mat_Env_GravelPlot", new Vector2(4f, 3f)), false);
            Part(PrimitiveType.Cube, "Gravel Path", parent, new Vector3(0f, 0.012f, -5.7f), new Vector3(2.2f, 0.02f, 12.6f), GravelMaterial("Mat_Env_GravelPath", new Vector2(1f, 5.5f)), false);
        }

        private static void CreateFence(Transform parent)
        {
            Transform fence = Group("Fence", parent);
            Material wood = DarkWoodMaterial();

            FenceLine(fence, new Vector3(-FenceHalfWidth, 0f, FenceBack), new Vector3(FenceHalfWidth, 0f, FenceBack), wood);
            FenceLine(fence, new Vector3(-FenceHalfWidth, 0f, FenceFront), new Vector3(-FenceHalfWidth, 0f, FenceBack), wood);
            FenceLine(fence, new Vector3(FenceHalfWidth, 0f, FenceFront), new Vector3(FenceHalfWidth, 0f, FenceBack), wood);
            FenceLine(fence, new Vector3(-FenceHalfWidth, 0f, FenceFront), new Vector3(-GateHalfWidth, 0f, FenceFront), wood);
            FenceLine(fence, new Vector3(GateHalfWidth, 0f, FenceFront), new Vector3(FenceHalfWidth, 0f, FenceFront), wood);
        }

        // posts about every 1.8 m, two rails between them
        private static void FenceLine(Transform parent, Vector3 from, Vector3 to, Material wood)
        {
            Vector3 direction = to - from;
            int sections = Mathf.Max(1, Mathf.CeilToInt(direction.magnitude / 1.8f));
            Quaternion along = Quaternion.LookRotation(direction.normalized);

            for (int i = 0; i <= sections; i++)
            {
                Vector3 post = Vector3.Lerp(from, to, i / (float)sections);
                Part(PrimitiveType.Cube, "Post", parent, post + new Vector3(0f, 0.55f, 0f), new Vector3(0.14f, 1.1f, 0.14f), wood, true);
            }

            float length = direction.magnitude;
            Vector3 middle = (from + to) / 2f;
            foreach (float height in new[] { 0.45f, 0.85f })
            {
                GameObject rail = Part(PrimitiveType.Cube, "Rail", parent, middle + new Vector3(0f, height, 0f), new Vector3(0.06f, 0.1f, length), wood, true);
                rail.transform.rotation = along;
            }
        }

        private static void CreateShed(Transform parent, Vector3 position)
        {
            Transform shed = Group("Shed", parent);
            shed.position = position;
            shed.rotation = Quaternion.Euler(0f, 90f, 0f);

            Material wood = WoodMaterial();
            Material roof = Plain("Mat_Env_Roof", new Color(0.62f, 0.27f, 0.2f), 0.15f);
            Material door = DarkWoodMaterial();

            Part(PrimitiveType.Cube, "Walls", shed, new Vector3(0f, 1f, 0f), new Vector3(2.6f, 2f, 2f), wood, true);
            Part(PrimitiveType.Cube, "Door", shed, new Vector3(0f, 0.75f, 1.02f), new Vector3(0.8f, 1.5f, 0.05f), door, false);

            // gable under the roof (a cube turned 45 degrees) and the two roof sides
            GameObject gable = Part(PrimitiveType.Cube, "Gable", shed, new Vector3(0f, 2f, 0f), new Vector3(2.6f, 1.41f, 1.41f), wood, false);
            gable.transform.localRotation = Quaternion.Euler(45f, 0f, 0f);
            GameObject front = Part(PrimitiveType.Cube, "Roof Front", shed, new Vector3(0f, 2.55f, 0.55f), new Vector3(3f, 0.1f, 1.6f), roof, false);
            front.transform.localRotation = Quaternion.Euler(40f, 0f, 0f);
            GameObject back = Part(PrimitiveType.Cube, "Roof Back", shed, new Vector3(0f, 2.55f, -0.55f), new Vector3(3f, 0.1f, 1.6f), roof, false);
            back.transform.localRotation = Quaternion.Euler(-40f, 0f, 0f);
        }

        // a flower border along the back and the sides of the fence
        private static void CreateFlowers(Transform parent, Random random)
        {
            Transform flowers = Group("Flowers", parent);
            Material stem = Plain("Mat_Env_Stem", new Color(0.3f, 0.55f, 0.2f), 0f);
            Material[] petals =
            {
                Plain("Mat_Env_FlowerRed", new Color(0.9f, 0.25f, 0.25f), 0.1f),
                Plain("Mat_Env_FlowerYellow", new Color(1f, 0.85f, 0.25f), 0.1f),
                Plain("Mat_Env_FlowerWhite", new Color(0.95f, 0.95f, 0.92f), 0.1f),
                Plain("Mat_Env_FlowerPurple", new Color(0.62f, 0.4f, 0.85f), 0.1f)
            };

            int placed = 0;
            for (int attempt = 0; attempt < 200 && placed < 45; attempt++)
            {
                Vector3 position;
                int side = random.Next(3);
                if (side == 0) position = new Vector3(Range(random, -6.5f, 6.5f), 0f, Range(random, 9.6f, 10.1f));
                else position = new Vector3((side == 1 ? -1f : 1f) * Range(random, 6.2f, 6.6f), 0f, Range(random, -1f, 10f));

                // not in front of the shed door
                if (position.x < -3.2f && position.z > 6.5f) continue;

                Transform flower = Group("Flower", flowers);
                flower.position = position;
                float height = Range(random, 0.22f, 0.4f);
                Part(PrimitiveType.Cylinder, "Stem", flower, new Vector3(0f, height / 2f, 0f), new Vector3(0.03f, height / 2f, 0.03f), stem, false);
                Part(PrimitiveType.Sphere, "Petals", flower, new Vector3(0f, height, 0f), Vector3.one * Range(random, 0.12f, 0.18f), petals[random.Next(petals.Length)], false);
                placed++;
            }
        }

        // round and pointed trees in a ring around the garden, leaving the path free
        private static void CreateTrees(Transform parent, Random random)
        {
            Transform trees = Group("Trees", parent);
            Material bark = DarkWoodMaterial();
            Material[] leaves =
            {
                Plain("Mat_Env_Leaves1", new Color(0.3f, 0.55f, 0.22f), 0f),
                Plain("Mat_Env_Leaves2", new Color(0.4f, 0.62f, 0.25f), 0f),
                Plain("Mat_Env_Leaves3", new Color(0.22f, 0.45f, 0.25f), 0f)
            };

            int placed = 0;
            for (int attempt = 0; attempt < 200 && placed < 28; attempt++)
            {
                float angle = Range(random, 0f, Mathf.PI * 2f);
                float radius = Range(random, 12f, 26f);
                Vector3 position = new Vector3(Mathf.Cos(angle) * radius, 0f, 4f + Mathf.Sin(angle) * radius);
                if (position.z < 0f && Mathf.Abs(position.x) < 4f) continue;

                Transform tree = Group("Tree", trees);
                tree.position = position;
                tree.rotation = Quaternion.Euler(0f, Range(random, 0f, 360f), 0f);
                tree.localScale = Vector3.one * Range(random, 0.8f, 1.35f);
                Material leaf = leaves[random.Next(leaves.Length)];

                if (random.NextDouble() < 0.6)
                {
                    float trunk = Range(random, 1.6f, 2.4f);
                    Part(PrimitiveType.Cylinder, "Trunk", tree, new Vector3(0f, trunk / 2f, 0f), new Vector3(0.35f, trunk / 2f, 0.35f), bark, true);
                    Part(PrimitiveType.Sphere, "Crown", tree, new Vector3(0f, trunk + 0.8f, 0f), Vector3.one * 2.8f, leaf, false);
                    Part(PrimitiveType.Sphere, "Crown", tree, new Vector3(0.7f, trunk + 0.4f, 0.3f), Vector3.one * 2f, leaf, false);
                    Part(PrimitiveType.Sphere, "Crown", tree, new Vector3(-0.6f, trunk + 0.5f, -0.4f), Vector3.one * 2.1f, leaf, false);
                }
                else
                {
                    Part(PrimitiveType.Cylinder, "Trunk", tree, new Vector3(0f, 0.6f, 0f), new Vector3(0.3f, 0.6f, 0.3f), bark, true);
                    Part(PrimitiveType.Sphere, "Tier", tree, new Vector3(0f, 1.6f, 0f), new Vector3(2.6f, 1.4f, 2.6f), leaves[2], false);
                    Part(PrimitiveType.Sphere, "Tier", tree, new Vector3(0f, 2.6f, 0f), new Vector3(1.9f, 1.3f, 1.9f), leaves[2], false);
                    Part(PrimitiveType.Sphere, "Tier", tree, new Vector3(0f, 3.5f, 0f), new Vector3(1.1f, 1.2f, 1.1f), leaves[2], false);
                }
                placed++;
            }
        }

        // bushes just outside the fence
        private static void CreateBushes(Transform parent, Random random)
        {
            Transform bushes = Group("Bushes", parent);
            Material[] leaves =
            {
                Plain("Mat_Env_Leaves1", new Color(0.3f, 0.55f, 0.22f), 0f),
                Plain("Mat_Env_Leaves2", new Color(0.4f, 0.62f, 0.25f), 0f)
            };

            for (int i = 0; i < 16; i++)
            {
                Vector3 position;
                int side = random.Next(3);
                if (side == 0) position = new Vector3(Range(random, -8f, 8f), 0f, FenceBack + Range(random, 0.8f, 1.6f));
                else position = new Vector3((side == 1 ? -1f : 1f) * (FenceHalfWidth + Range(random, 0.8f, 1.6f)), 0f, Range(random, -1f, 10f));

                Transform bush = Group("Bush", bushes);
                bush.position = position;
                Material leaf = leaves[random.Next(leaves.Length)];
                int balls = 2 + random.Next(2);
                for (int b = 0; b < balls; b++)
                {
                    float size = Range(random, 0.7f, 1.2f);
                    Vector3 offset = new Vector3(Range(random, -0.4f, 0.4f), size * 0.4f, Range(random, -0.4f, 0.4f));
                    Part(PrimitiveType.Sphere, "Leaves", bush, offset, new Vector3(size, size * 0.8f, size), leaf, false);
                }
            }
        }

        private static void CreateRocks(Transform parent, Random random)
        {
            Transform rocks = Group("Rocks", parent);
            Material stone = Plain("Mat_Env_Rock", new Color(0.55f, 0.55f, 0.52f), 0.1f);

            for (int i = 0; i < 12; i++)
            {
                float angle = Range(random, 0f, Mathf.PI * 2f);
                float radius = Range(random, 9.5f, 22f);
                Vector3 position = new Vector3(Mathf.Cos(angle) * radius, 0.05f, 4f + Mathf.Sin(angle) * radius);
                if (position.z < 0f && Mathf.Abs(position.x) < 3f) continue;

                GameObject rock = Part(PrimitiveType.Sphere, "Rock", rocks, position, new Vector3(Range(random, 0.6f, 1.4f), Range(random, 0.35f, 0.7f), Range(random, 0.6f, 1.2f)), stone, false);
                rock.transform.rotation = Quaternion.Euler(Range(random, -10f, 10f), Range(random, 0f, 360f), Range(random, -10f, 10f));
            }
        }

        // soft green hills on the horizon
        private static void CreateHills(Transform parent, Random random)
        {
            Transform hills = Group("Hills", parent);
            Material[] grass =
            {
                Plain("Mat_Env_Hill1", new Color(0.42f, 0.58f, 0.32f), 0f),
                Plain("Mat_Env_Hill2", new Color(0.36f, 0.52f, 0.3f), 0f)
            };

            for (int i = 0; i < 10; i++)
            {
                float angle = i / 10f * Mathf.PI * 2f + Range(random, -0.2f, 0.2f);
                float radius = Range(random, 48f, 58f);
                Vector3 position = new Vector3(Mathf.Cos(angle) * radius, -4f, 4f + Mathf.Sin(angle) * radius);
                Part(PrimitiveType.Sphere, "Hill", hills, position, new Vector3(Range(random, 30f, 45f), Range(random, 10f, 18f), Range(random, 30f, 45f)), grass[i % 2], false);
            }
        }

        // ================= textures =================

        private static Texture2D GetTexture(string name, Func<int, int, Color> pixel)
        {
            string path = ArtFolder + "/" + name + ".png";
            Texture2D existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (existing != null) return existing;

            EnsureFolder(ArtFolder);
            Texture2D texture = new Texture2D(TextureSize, TextureSize, TextureFormat.RGB24, false);
            Color[] pixels = new Color[TextureSize * TextureSize];
            for (int y = 0; y < TextureSize; y++)
            {
                for (int x = 0; x < TextureSize; x++)
                {
                    pixels[y * TextureSize + x] = pixel(x, y);
                }
            }
            texture.SetPixels(pixels);
            texture.Apply();

            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path);

            TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.mipmapEnabled = true;
            importer.anisoLevel = 4;
            importer.SaveAndReimport();

            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static Color GrassPixel(int x, int y)
        {
            float u = x / (float)TextureSize;
            float v = y / (float)TextureSize;
            float large = Fbm(u, v, 3, 3, 11f);
            float blades = Noise(u, v, 64, 8, 23f);
            float speck = Hash(x, y, 3);

            Color dark = new Color(0.27f, 0.47f, 0.17f);
            Color light = new Color(0.47f, 0.66f, 0.26f);
            Color color = Color.Lerp(dark, light, large * 0.7f + blades * 0.3f);
            return color * (0.9f + speck * 0.15f);
        }

        private static Color SoilPixel(int x, int y)
        {
            float u = x / (float)TextureSize;
            float v = y / (float)TextureSize;
            float clumps = Fbm(u, v, 6, 3, 37f);
            float grain = Hash(x, y, 5);
            float value = 0.78f + clumps * 0.2f + (grain - 0.5f) * 0.1f;

            // small pebbles
            if (Hash(x / 3, y / 3, 9) > 0.975f) value = 1f;
            return new Color(value, value * 0.97f, value * 0.93f);
        }

        private static Color GravelPixel(int x, int y)
        {
            float u = x / (float)TextureSize;
            float v = y / (float)TextureSize;
            float pebble = Hash(x / 4, y / 4, 13);
            float shade = Fbm(u, v, 8, 2, 41f);
            float grain = Hash(x, y, 17);

            Color baseColor = new Color(0.74f, 0.69f, 0.6f);
            return baseColor * (0.72f + pebble * 0.25f + shade * 0.12f + (grain - 0.5f) * 0.08f);
        }

        // horizontal planks, 4 per texture, with grain and a dark seam
        private static Color WoodPixel(int x, int y)
        {
            float u = x / (float)TextureSize;
            float v = y / (float)TextureSize;
            int plank = y / (TextureSize / 4);
            float tone = 0.85f + Hash(plank, 0, 29) * 0.25f;
            float grain = Noise(u, v, 2, 48, 53f);
            bool seam = y % (TextureSize / 4) < 2;

            Color wood = new Color(0.62f, 0.43f, 0.26f) * tone * (0.78f + grain * 0.3f);
            return seam ? wood * 0.55f : wood;
        }

        // ================= noise helpers (all tile seamlessly) =================

        // Perlin noise blended from 4 samples so that the right edge matches the left one, and the top the bottom
        private static float Noise(float u, float v, int scaleX, int scaleY, float seed)
        {
            float x = u * scaleX;
            float y = v * scaleY;
            float a = Mathf.PerlinNoise(x + seed, y + seed);
            float b = Mathf.PerlinNoise(x - scaleX + seed, y + seed);
            float c = Mathf.PerlinNoise(x + seed, y - scaleY + seed);
            float d = Mathf.PerlinNoise(x - scaleX + seed, y - scaleY + seed);
            return a * (1f - u) * (1f - v) + b * u * (1f - v) + c * (1f - u) * v + d * u * v;
        }

        private static float Fbm(float u, float v, int scale, int octaves, float seed)
        {
            float sum = 0f;
            float weight = 0.5f;
            float total = 0f;
            for (int i = 0; i < octaves; i++)
            {
                sum += Noise(u, v, scale, scale, seed + i * 17f) * weight;
                total += weight;
                scale *= 2;
                weight *= 0.5f;
            }
            return sum / total;
        }

        // repeatable random value between 0 and 1 for a pixel or a cell
        private static float Hash(int x, int y, int seed)
        {
            unchecked
            {
                int h = x * 374761393 + y * 668265263 + seed * 144269504;
                h = (h ^ (h >> 13)) * 1274126177;
                h ^= h >> 16;
                return (h & 0xFFFF) / 65535f;
            }
        }

        // ================= helpers =================

        private static GameObject Part(PrimitiveType type, string name, Transform parent, Vector3 localPosition, Vector3 localScale, Material material, bool keepCollider)
        {
            GameObject part = GameObject.CreatePrimitive(type);
            part.name = name;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = localPosition;
            part.transform.localScale = localScale;
            part.GetComponent<Renderer>().sharedMaterial = material;
            if (!keepCollider) Object.DestroyImmediate(part.GetComponent<Collider>());
            return part;
        }

        private static Transform Group(string name, Transform parent)
        {
            Transform group = new GameObject(name).transform;
            group.SetParent(parent, false);
            return group;
        }

        private static float Range(Random random, float min, float max)
        {
            return min + (float)random.NextDouble() * (max - min);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
