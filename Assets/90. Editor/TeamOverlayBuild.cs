using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace TeamOverlay.Editor
{
    public static class TeamOverlayBuild
    {
        private const string SceneFolder = "Assets/00. Scenes";
        private const string ScenePath = SceneFolder + "/TeamOverlay.unity";
        private const string BuildPath = "Builds/Windows/DOTORI ON.exe";

        [MenuItem("Team Overlay/Configure Project")]
        public static void ConfigureProject()
        {
            if (!TryConfigureProject())
            {
                Debug.LogWarning("Team Overlay configuration was cancelled because an open scene has unsaved changes.");
            }
        }

        [MenuItem("Team Overlay/Build Windows x86_64")]
        public static void BuildWindows()
        {
            if (!TryConfigureProject())
            {
                return;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(BuildPath) ?? "Builds/Windows");
            var options = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = BuildPath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            };

            var report = BuildPipeline.BuildPlayer(options);
            var summary = report.summary;
            if (summary.result != BuildResult.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Team Overlay Windows build failed: {summary.result} ({summary.totalErrors} errors)");
            }

            Debug.Log($"Team Overlay Windows build completed: {Path.GetFullPath(BuildPath)}");
        }

        public static void ConfigureProjectFromCommandLine()
        {
            if (!TryConfigureProject())
            {
                throw new InvalidOperationException("Could not configure the Team Overlay project.");
            }
        }

        public static void BuildWindowsFromCommandLine()
        {
            BuildWindows();
        }

        private static bool TryConfigureProject()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return false;
            }

            PlayerSettings.companyName = "DotoriDoguldan";
            PlayerSettings.productName = "DOTORI ON";
            PlayerSettings.defaultScreenWidth = 480;
            PlayerSettings.defaultScreenHeight = 220;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.resizableWindow = false;
            PlayerSettings.allowFullscreenSwitch = false;
            PlayerSettings.runInBackground = true;
            PlayerSettings.forceSingleInstance = true;
            PlayerSettings.usePlayerLog = true;

            // Re-asserted on every build because the Editor silently restores the
            // splash under some licences, and it kept coming back after being
            // switched off by hand.
            PlayerSettings.SplashScreen.show = false;
            PlayerSettings.SplashScreen.showUnityLogo = false;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
            QualitySettings.vSyncCount = 0;

            // A 480x220 2D overlay gains nothing from D3D12, and the run that froze
            // logged "IDXGISwapChain::GetFrameStatistics is broken" repeatedly —
            // this app rewrites its own window style after the swapchain exists,
            // which D3D12 does not take well. Pinned so the shipped build behaves
            // like a launch with -force-d3d11.
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.StandaloneWindows64, false);
            PlayerSettings.SetGraphicsAPIs(
                BuildTarget.StandaloneWindows64,
                new[] { GraphicsDeviceType.Direct3D11 });

            EnsureFolder("Assets", "00. Scenes");
            EnsureSceneExists();

            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(ScenePath, true)
            };

            AssetDatabase.SaveAssets();
            Debug.Log("Team Overlay project configured for a 480x220 Windows mock build.");
            return true;
        }

        private static void EnsureSceneExists()
        {
            if (File.Exists(ScenePath))
            {
                return;
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            cameraObject.tag = "MainCamera";
            var camera = cameraObject.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.063f, 0.086f, 0.129f, 1f);
            camera.orthographic = true;
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);

            if (!EditorSceneManager.SaveScene(scene, ScenePath))
            {
                throw new InvalidOperationException("Failed to create the Team Overlay bootstrap scene.");
            }
        }

        private static void EnsureFolder(string parent, string child)
        {
            var path = parent + "/" + child;
            if (!AssetDatabase.IsValidFolder(path))
            {
                AssetDatabase.CreateFolder(parent, child);
            }
        }
    }
}
