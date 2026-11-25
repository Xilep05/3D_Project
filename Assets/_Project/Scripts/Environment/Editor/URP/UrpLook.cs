using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace GardenWise
{
    // URP look of the game (only compiled once the URP package is installed: see GardenWise.URP.Editor.asmdef)
    // - pipeline: 4x MSAA, shadows up to 45 m, HDR, ambient occlusion (SSAO) on the renderer
    // - post-processing profile: soft bloom, neutral tone mapping, a bit more contrast and colour, warm white balance, light vignette
    // - every scene built by GardenWise gets a global Volume and post-processing on its cameras
    // - when the project targets Android (Meta Quest): no SSAO, no HDR, no post-processing, shorter shadows
    public static class UrpLook
    {
        private const string ProfilePath = "Assets/_Project/Art/Environment/PostFX_Profile.asset";

        [MenuItem("GardenWise/Setup URP Look (open scene)")]
        public static void SetupFromMenu()
        {
            ApplyToOpenScene();
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log("URP look applied. Save the scene (Cmd+S).");
        }

        // called by name by EnvironmentArt.FinishScene at the end of every scene builder
        public static void ApplyToOpenScene()
        {
            ConfigurePipeline();

            VolumeProfile profile = GetProfile();
            Volume volume = Object.FindFirstObjectByType<Volume>();
            if (volume == null) volume = new GameObject("Global Volume").AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = profile;

            foreach (Camera camera in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
            {
                UniversalAdditionalCameraData data = camera.GetUniversalAdditionalCameraData();
                data.renderPostProcessing = !IsQuest();
            }
        }

        // the Quest renders natively on Android: full-screen effects cost too much there
        private static bool IsQuest()
        {
            return EditorUserBuildSettings.activeBuildTarget == BuildTarget.Android;
        }

        private static void ConfigurePipeline()
        {
            UniversalRenderPipelineAsset asset = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (asset == null) asset = GraphicsSettings.defaultRenderPipeline as UniversalRenderPipelineAsset;
            if (asset == null)
            {
                Debug.LogWarning("No URP asset in Project Settings > Graphics: run Window > Rendering > Render Pipeline Converter first.");
                return;
            }

            bool quest = IsQuest();
            asset.msaaSampleCount = 4;
            asset.shadowDistance = quest ? 20f : 45f;
            asset.supportsHDR = !quest;
            EditorUtility.SetDirty(asset);

            SerializedProperty renderers = new SerializedObject(asset).FindProperty("m_RendererDataList");
            if (renderers != null && renderers.arraySize > 0)
            {
                ScriptableRendererData data = renderers.GetArrayElementAtIndex(0).objectReferenceValue as ScriptableRendererData;
                if (data != null) SetAmbientOcclusion(data, !quest);
            }
            AssetDatabase.SaveAssets();
        }

        // switches SSAO on (PC) or off (Quest); adds it the same way as the "Add Renderer Feature" button if missing
        private static void SetAmbientOcclusion(ScriptableRendererData data, bool enabled)
        {
            Type ssaoType = Type.GetType("UnityEngine.Rendering.Universal.ScreenSpaceAmbientOcclusion, Unity.RenderPipelines.Universal.Runtime");
            if (ssaoType == null) return;

            foreach (ScriptableRendererFeature existing in data.rendererFeatures)
            {
                if (existing != null && existing.GetType() == ssaoType)
                {
                    existing.SetActive(enabled);
                    EditorUtility.SetDirty(existing);
                    EditorUtility.SetDirty(data);
                    return;
                }
            }
            if (!enabled) return;

            ScriptableRendererFeature feature = (ScriptableRendererFeature)ScriptableObject.CreateInstance(ssaoType);
            feature.name = "ScreenSpaceAmbientOcclusion";
            AssetDatabase.AddObjectToAsset(feature, data);

            string guid;
            long localId;
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out guid, out localId);

            SerializedObject serialized = new SerializedObject(data);
            SerializedProperty features = serialized.FindProperty("m_RendererFeatures");
            SerializedProperty map = serialized.FindProperty("m_RendererFeatureMap");
            features.arraySize++;
            features.GetArrayElementAtIndex(features.arraySize - 1).objectReferenceValue = feature;
            if (map != null)
            {
                map.arraySize++;
                map.GetArrayElementAtIndex(map.arraySize - 1).longValue = localId;
            }
            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(data);
        }

        private static VolumeProfile GetProfile()
        {
            VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
            if (profile != null) return profile;

            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, ProfilePath);

            Bloom bloom = Add<Bloom>(profile);
            bloom.intensity.Override(0.35f);
            bloom.threshold.Override(1.05f);
            bloom.scatter.Override(0.6f);

            Tonemapping tonemapping = Add<Tonemapping>(profile);
            tonemapping.mode.Override(TonemappingMode.Neutral);

            ColorAdjustments colour = Add<ColorAdjustments>(profile);
            colour.postExposure.Override(0.15f);
            colour.contrast.Override(12f);
            colour.saturation.Override(15f);

            WhiteBalance white = Add<WhiteBalance>(profile);
            white.temperature.Override(8f);

            Vignette vignette = Add<Vignette>(profile);
            vignette.intensity.Override(0.22f);
            vignette.smoothness.Override(0.45f);

            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            return profile;
        }

        private static T Add<T>(VolumeProfile profile) where T : VolumeComponent
        {
            T component = profile.Add<T>(true);
            component.name = typeof(T).Name;
            AssetDatabase.AddObjectToAsset(component, profile);
            return component;
        }
    }
}
