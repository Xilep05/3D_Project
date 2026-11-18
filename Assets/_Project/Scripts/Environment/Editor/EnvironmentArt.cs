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
        private const string NatureModels = "Assets/_Project/Art/ThirdParty/Quaternius_Nature/Models";
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
            // free CC0 models of Quaternius when they are in Art/ThirdParty, simple shapes otherwise
            ThirdPartyImport.FixAllMaterials();

            GameObject root = new GameObject("Environment");
            Random random = new Random(7);

            CreateGroundAndPaths(root.transform);
            CreateFence(root.transform);
            CreateShed(root.transform, new Vector3(-5f, 0f, 8.4f));
            CreateFlowers(root.transform, random);
            CreateTrees(root.transform, random);
            CreateBushes(root.transform, random);
            CreateRocks(root.transform, random);
            CreateGrassTufts(root.transform, random);
            CreatePathPebbles(root.transform, random);
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

            GameObject[] flowerModels = Models("Flower_3_Group", "Flower_4_Group", "Flower_3_Group", "Clover_1", "Plant_7");
            if (flowerModels.Length > 0)
            {
                for (int i = 0; i < 40; i++)
                {
                    Vector3 spot = BorderSpot(random);
                    if (spot.x < -3.2f && spot.z > 6.5f) continue;
                    Spawn(flowerModels[random.Next(flowerModels.Length)], flowers, spot, Range(random, 0.3f, 0.5f), random);
                }
                return;
            }

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

            GameObject[] broadleaf = Models("CommonTree_1", "CommonTree_2", "CommonTree_3", "CommonTree_4", "CommonTree_5");
            GameObject[] pines = Models("Pine_1", "Pine_2", "Pine_3");
            GameObject[] undergrowth = Models("Fern_1", "Mushroom_Common", "Plant_7");
            if (broadleaf.Length > 0 || pines.Length > 0)
            {
                int count = 0;
                for (int attempt = 0; attempt < 200 && count < 32; attempt++)
                {
                    float angle = Range(random, 0f, Mathf.PI * 2f);
                    float radius = Range(random, 12f, 28f);
                    Vector3 spot = new Vector3(Mathf.Cos(angle) * radius, 0f, 4f + Mathf.Sin(angle) * radius);
                    if (spot.z < 0f && Mathf.Abs(spot.x) < 4f) continue;

                    bool pine = pines.Length > 0 && (broadleaf.Length == 0 || random.NextDouble() < 0.35);
                    GameObject model = pine ? pines[random.Next(pines.Length)] : broadleaf[random.Next(broadleaf.Length)];
                    Spawn(model, trees, spot, pine ? Range(random, 6f, 9f) : Range(random, 4.5f, 7f), random);
                    TrunkCollider(trees, spot);

                    // ferns and mushrooms at the foot of some trees
                    if (undergrowth.Length > 0 && random.NextDouble() < 0.5)
                    {
                        Vector3 foot = spot + new Vector3(Range(random, -1.5f, 1.5f), 0f, Range(random, -1.5f, 1.5f));
                        GameObject plant = undergrowth[random.Next(undergrowth.Length)];
                        Spawn(plant, trees, foot, plant.name.StartsWith("Mushroom") ? 0.25f : 0.7f, random);
                    }
                    count++;
                }
                return;
            }

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

            GameObject[] bushModels = Models("Bush_Common", "Bush_Common_Flowers");
            if (bushModels.Length > 0)
            {
                for (int i = 0; i < 18; i++)
                {
                    Spawn(bushModels[random.Next(bushModels.Length)], bushes, OutsideFenceSpot(random), Range(random, 0.9f, 1.5f), random);
                }
                return;
            }

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

            GameObject[] rockModels = Models("Rock_Medium_1", "Rock_Medium_2", "Rock_Medium_3");
            if (rockModels.Length > 0)
            {
                for (int i = 0; i < 14; i++)
                {
                    float angle = Range(random, 0f, Mathf.PI * 2f);
                    float radius = Range(random, 9.5f, 24f);
                    Vector3 spot = new Vector3(Mathf.Cos(angle) * radius, 0f, 4f + Mathf.Sin(angle) * radius);
                    if (spot.z < 0f && Mathf.Abs(spot.x) < 3f) continue;
                    Spawn(rockModels[random.Next(rockModels.Length)], rocks, spot, Range(random, 0.4f, 1.1f), random);
                }
                return;
            }

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

        // tufts of grass on the lawn, never on the gravel or in the garden
        private static void CreateGrassTufts(Transform parent, Random random)
        {
            GameObject[] grass = Models("Grass_Common_Short", "Grass_Common_Tall", "Grass_Wispy_Short", "Grass_Common_Short");
            if (grass.Length == 0) return;

            Transform tufts = Group("Grass Tufts", parent);
            int placed = 0;
            for (int attempt = 0; attempt < 600 && placed < 170; attempt++)
            {
                float angle = Range(random, 0f, Mathf.PI * 2f);
                float radius = Mathf.Sqrt(Range(random, 0.02f, 1f)) * 26f;
                Vector3 spot = new Vector3(Mathf.Cos(angle) * radius, 0f, 4f + Mathf.Sin(angle) * radius);
                if (OnGravel(spot)) continue;

                Spawn(grass[random.Next(grass.Length)], tufts, spot, Range(random, 0.25f, 0.55f), random);
                placed++;
            }
        }

        // round pebbles along both sides of the path
        private static void CreatePathPebbles(Transform parent, Random random)
        {
            GameObject[] pebbles = Models("Pebble_Round_1", "Pebble_Round_2", "Pebble_Round_3");
            if (pebbles.Length == 0) return;

            Transform group = Group("Path Pebbles", parent);
            for (float z = -11.5f; z < -2.2f; z += Range(random, 0.9f, 1.6f))
            {
                float side = random.NextDouble() < 0.5 ? -1f : 1f;
                Vector3 spot = new Vector3(side * Range(random, 1.2f, 1.45f), 0f, z);
                Spawn(pebbles[random.Next(pebbles.Length)], group, spot, Range(random, 0.12f, 0.25f), random);
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

        // ================= third-party models =================

        // the models found in Art/ThirdParty/Quaternius_Nature/Models (missing ones are skipped)
        private static GameObject[] Models(params string[] names)
        {
            System.Collections.Generic.List<GameObject> found = new System.Collections.Generic.List<GameObject>();
            foreach (string name in names)
            {
                GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(NatureModels + "/" + name + ".fbx");
                if (model != null) found.Add(model);
            }
            return found.ToArray();
        }

        // places a model with a given height, a random turn, its base on the ground
        private static GameObject Spawn(GameObject model, Transform parent, Vector3 position, float height, Random random)
        {
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(model, parent);
            instance.transform.position = position;
            // keep the model's own axis correction (FBX files often come turned by -90 on x)
            instance.transform.rotation = Quaternion.Euler(0f, Range(random, 0f, 360f), 0f) * model.transform.rotation;

            Bounds bounds = RendererBounds(instance);
            if (bounds.size.y > 0.001f) instance.transform.localScale = instance.transform.localScale * (height / bounds.size.y);

            bounds = RendererBounds(instance);
            instance.transform.position += new Vector3(0f, position.y - bounds.min.y, 0f);
            return instance;
        }

        // the models have no collider: an invisible trunk stops the player walking through a tree
        private static void TrunkCollider(Transform parent, Vector3 position)
        {
            GameObject trunk = new GameObject("Trunk Collider");
            trunk.transform.SetParent(parent, false);
            trunk.transform.position = position + new Vector3(0f, 1.5f, 0f);
            CapsuleCollider capsule = trunk.AddComponent<CapsuleCollider>();
            capsule.radius = 0.35f;
            capsule.height = 3f;
        }

        private static Bounds RendererBounds(GameObject instance)
        {
            Renderer[] renderers = instance.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return new Bounds(instance.transform.position, Vector3.zero);

            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            return bounds;
        }

        // a spot in the flower border, along the back and the sides inside the fence
        private static Vector3 BorderSpot(Random random)
        {
            int side = random.Next(3);
            if (side == 0) return new Vector3(Range(random, -6.5f, 6.5f), 0f, Range(random, 9.5f, 10.1f));
            return new Vector3((side == 1 ? -1f : 1f) * Range(random, 6.1f, 6.6f), 0f, Range(random, -1f, 10f));
        }

        private static Vector3 OutsideFenceSpot(Random random)
        {
            int side = random.Next(3);
            if (side == 0) return new Vector3(Range(random, -8f, 8f), 0f, FenceBack + Range(random, 0.8f, 1.6f));
            return new Vector3((side == 1 ? -1f : 1f) * (FenceHalfWidth + Range(random, 0.8f, 1.6f)), 0f, Range(random, -1f, 10f));
        }

        // the gravel plot (garden, barrel, bell) and the path
        private static bool OnGravel(Vector3 spot)
        {
            bool plot = Mathf.Abs(spot.x) < 4.8f && spot.z > 0.4f && spot.z < 7.6f;
            bool path = Mathf.Abs(spot.x) < 1.5f && spot.z < 0.6f && spot.z > -12.2f;
            bool shed = spot.x < -3.4f && spot.x > -6.6f && spot.z > 6.8f && spot.z < 10f;
            return plot || path || shed;
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
