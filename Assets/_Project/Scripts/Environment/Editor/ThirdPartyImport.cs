using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace GardenWise
{
    // import settings for the free CC0 models of Quaternius in Art/ThirdParty, applied automatically:
    // - one editable material per texture (nature kit) or per model material (crops), in a Materials folder
    // - textures found in the Textures folder, at most 1024 px (lighter for the Quest)
    // - leaves, grass and petals use the GardenWise/Foliage shader (cut-out, two-sided), everything else is matt
    public class ThirdPartyImport : AssetPostprocessor
    {
        public const string ThirdPartyRoot = "Assets/_Project/Art/ThirdParty";
        private const string NatureFolder = ThirdPartyRoot + "/Quaternius_Nature";

        private void OnPreprocessModel()
        {
            if (!assetPath.StartsWith(ThirdPartyRoot)) return;

            ModelImporter importer = (ModelImporter)assetImporter;
            importer.importAnimation = false;
            importer.importCameras = false;
            importer.importLights = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            importer.materialLocation = ModelImporterMaterialLocation.External;
            importer.materialSearch = ModelImporterMaterialSearch.RecursiveUp;
            importer.materialName = assetPath.StartsWith(NatureFolder)
                ? ModelImporterMaterialName.BasedOnTextureName
                : ModelImporterMaterialName.BasedOnModelNameAndMaterialName;
        }

        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(ThirdPartyRoot)) return;

            TextureImporter importer = (TextureImporter)assetImporter;
            importer.maxTextureSize = 1024;
            if (IsFoliage(Path.GetFileNameWithoutExtension(assetPath))) importer.alphaIsTransparency = true;
        }

        private void OnPostprocessMaterial(Material material)
        {
            if (assetPath.StartsWith(ThirdPartyRoot)) Setup(material);
        }

        // menu in case the materials were created before the Foliage shader was imported
        [MenuItem("GardenWise/Fix Third-Party Materials")]
        public static void FixAllMaterials()
        {
            if (!AssetDatabase.IsValidFolder(ThirdPartyRoot)) return;

            // files copied while Unity was open were imported before this script existed: import them again
            foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { ThirdPartyRoot }))
            {
                ModelImporter model = AssetImporter.GetAtPath(AssetDatabase.GUIDToAssetPath(guid)) as ModelImporter;
                if (model != null && model.materialLocation != ModelImporterMaterialLocation.External) model.SaveAndReimport();
            }
            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { ThirdPartyRoot }))
            {
                TextureImporter texture = AssetImporter.GetAtPath(AssetDatabase.GUIDToAssetPath(guid)) as TextureImporter;
                if (texture != null && texture.maxTextureSize > 1024) texture.SaveAndReimport();
            }

            foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { ThirdPartyRoot }))
            {
                Material material = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
                if (material == null) continue;
                Setup(material);
                EditorUtility.SetDirty(material);
            }
            AssetDatabase.SaveAssets();
        }

        public static void Setup(Material material)
        {
            Texture texture = material.HasProperty("_MainTex") ? material.mainTexture : null;

            // nature materials are named after their texture: if the texture was not imported yet, find it now
            if (texture == null && material.HasProperty("_MainTex"))
            {
                texture = AssetDatabase.LoadAssetAtPath<Texture2D>(NatureFolder + "/Textures/" + material.name + ".png");
                if (texture != null) material.mainTexture = texture;
            }
            string name = texture != null ? texture.name : material.name;

            if (IsFoliage(name))
            {
                Shader foliage = Shader.Find(IsUrp() ? "GardenWise/FoliageURP" : "GardenWise/Foliage");
                if (foliage != null && material.shader != foliage)
                {
                    Color color = material.HasProperty("_Color") ? material.color : Color.white;
                    material.shader = foliage;
                    material.color = color;
                    material.mainTexture = texture;
                    material.SetFloat("_Cutoff", 0.4f);
                }
                return;
            }

            // with URP, a Standard material must use the URP Lit shader (same colour and texture)
            if (IsUrp() && material.shader != null && material.shader.name == "Standard")
            {
                Shader lit = Shader.Find("Universal Render Pipeline/Lit");
                if (lit != null)
                {
                    Color color = material.color;
                    material.shader = lit;
                    material.color = color;
                    material.mainTexture = texture;
                }
            }

            // stylised low-poly looks better matt than shiny
            if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", 0.05f);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.05f);
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", 0f);
        }

        public static bool IsUrp()
        {
            return GraphicsSettings.defaultRenderPipeline != null;
        }

        private static bool IsFoliage(string name)
        {
            string lower = name.ToLowerInvariant();
            return lower.Contains("leaves") || lower.Contains("leaf") || lower.Contains("grass") || lower.Contains("flowers");
        }
    }
}
