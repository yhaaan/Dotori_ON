using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace DOTORION.Editor
{
    public static class DOTORIONBuild
    {
        private const string SceneFolder = "Assets/00. Scenes";
        private const string ScenePath = SceneFolder + "/DOTORION.unity";
        private const string BuildPath = "Builds/Windows/DOTORI ON.exe";

        [MenuItem("DOTORI ON/Windows 빌드 (x86_64)")]
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
                    $"DOTORI ON Windows 빌드에 실패했습니다: {summary.result} (오류 {summary.totalErrors}개)");
            }

            Debug.Log($"DOTORI ON Windows 빌드를 마쳤습니다: {Path.GetFullPath(BuildPath)}");
        }

        public static void ConfigureProjectFromCommandLine()
        {
            if (!TryConfigureProject())
            {
                throw new InvalidOperationException("DOTORI ON 프로젝트를 설정하지 못했습니다.");
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

            // Windows는 이 값으로 사용자별 경로를 만듭니다. PlayerPrefs는
            // HKCU\Software\<company>\<product>에, Application.persistentDataPath는
            // AppData\LocalLow\<company>\<product>에 저장됩니다. 값을 바꾸면 둘 다 옮겨 가므로,
            // 이전 이름으로 실행하던 설치본은 처음 실행한 것처럼 보입니다.
            PlayerSettings.companyName = "Dotori Doguldan";
            PlayerSettings.productName = "DOTORI ON";
            PlayerSettings.defaultScreenWidth = 480;
            PlayerSettings.defaultScreenHeight = 220;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.resizableWindow = false;
            PlayerSettings.allowFullscreenSwitch = false;
            PlayerSettings.runInBackground = true;
            PlayerSettings.forceSingleInstance = true;
            PlayerSettings.usePlayerLog = true;

            // 빌드할 때마다 다시 꺼 둡니다. 일부 라이선스에서는 에디터가 스플래시를 몰래
            // 되살리고, 손으로 꺼도 계속 돌아왔습니다.
            PlayerSettings.SplashScreen.show = false;
            PlayerSettings.SplashScreen.showUnityLogo = false;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
            QualitySettings.vSyncCount = 0;

            // 480x220 2D 오버레이는 D3D12의 이점이 없습니다. 멈췄던 실행에서
            // "IDXGISwapChain::GetFrameStatistics is broken"이 반복해서 찍혔는데, 이 앱은
            // 스왑체인이 만들어진 뒤에 자기 창 스타일을 다시 쓰고, D3D12는 이를 잘 받아들이지
            // 못합니다. 배포 빌드가 -force-d3d11로 실행한 것과 같게 동작하도록 고정합니다.
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
            Debug.Log("DOTORI ON 프로젝트를 480x220 Windows 빌드용으로 설정했습니다.");
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
                throw new InvalidOperationException("DOTORI ON 시작 씬을 만들지 못했습니다.");
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
