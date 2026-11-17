using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

// Command line build for Quest, used as
//   Unity -batchmode -quit -projectPath . -executeMethod BuildGhostCoach.build_android
// The APK is written to Builds/GhostCoach.apk (Builds/ is gitignored).
public static class BuildGhostCoach {

    [MenuItem("GhostCoach/Build Android APK")]
    public static void build_android() {
        ensure_meta_project_config();
        preload_xr_settings();

        string[] scenes = EditorBuildSettings.scenes
            .Where(s => s.enabled)
            .Select(s => s.path)
            .ToArray();

        BuildPlayerOptions options = new BuildPlayerOptions();
        options.scenes = scenes;
        options.locationPathName = "Builds/GhostCoach.apk";
        options.target = BuildTarget.Android;
        options.options = BuildOptions.None;

        BuildReport report = BuildPipeline.BuildPlayer(options);
        BuildSummary summary = report.summary;
        Debug.Log("GhostCoach build " + summary.result
                  + ", " + summary.totalErrors + " errors, "
                  + (summary.totalSize / (1024*1024)) + " MB, "
                  + summary.totalTime.TotalSeconds.ToString("F0") + " s");

        if (Application.isBatchMode)
            EditorApplication.Exit(summary.result == BuildResult.Succeeded ? 0 : 1);
    }

    // The Meta SDK only creates Assets/Oculus/OculusProjectConfig.asset from an
    // editor update callback, which never runs in batch mode, and refuses to
    // create it during a build.  Without it the build skips the manifest
    // entries, including the passthrough feature.  So create it here.
    static void ensure_meta_project_config() {
        OVRProjectConfig config = OVRProjectConfig.CachedProjectConfig;
        if (config == null) {
            Debug.LogError("GhostCoach build: could not create OVRProjectConfig");
            return;
        }
        // Quest 1 support was dropped in Meta SDK v51.
        config.targetDeviceTypes = new List<OVRProjectConfig.DeviceType> {
            OVRProjectConfig.DeviceType.Quest2,
            OVRProjectConfig.DeviceType.QuestPro,
            OVRProjectConfig.DeviceType.Quest3 };
        // Passthrough is needed for mixed reality on the real table.
        config.insightPassthroughSupport = OVRProjectConfig.FeatureSupport.Supported;
        OVRProjectConfig.CommitProjectConfig(config);
        AssetDatabase.SaveAssets();
    }

    // In batch mode EditorBuildSettings.TryGetConfigObject() can return null
    // for XR settings that have not been loaded yet, which crashes the Meta
    // SDK build hooks.  Loading the assets first avoids that.
    static void preload_xr_settings() {
        AssetDatabase.LoadAllAssetsAtPath("Assets/XR/Settings/Oculus Settings.asset");
        AssetDatabase.LoadAllAssetsAtPath("Assets/XR/XRGeneralSettings.asset");
    }
}
