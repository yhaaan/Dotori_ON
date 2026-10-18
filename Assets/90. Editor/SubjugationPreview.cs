using System;
using System.IO;
using System.Reflection;
using DOTORION.Core;
using DOTORION.Events.Subjugation;
using DOTORION.Platform.Windows;
using DOTORION.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace DOTORION.Editor
{
    /// <summary>Offline visual QA without a sign-in, attendance write, or purchase on a real account.</summary>
    public static class SubjugationPreview
    {
        public static void ExportAndBuild()
        {
            Export();
            DOTORIONBuild.BuildWindowsFromCommandLine();
        }

        public static void Export()
        {
            Directory.CreateDirectory("Logs/ChiikawaPreview");
            using (var art = new SubjugationArt())
            using (var pixels = new SubjugationPixels(art))
            {
                if (!art.IsLoaded) throw new InvalidOperationException("Character atlas import failed.");
                pixels.SetScale(2);
                var game = new SubjugationGame(new SubjugationSave { coins = 9040 });
                game.Hire(1); game.Hire(2);
                for (var state = 0; state < 3; state++)
                {
                    pixels.Draw(game, state, 2);
                    File.WriteAllBytes("Logs/ChiikawaPreview/state-" + state + ".png", pixels.Texture.EncodeToPNG());
                }
                for (var i = 0; i < 6; i++) { game.Save.levels[i] = game.StartLevel(i); game.Save.party[i] = i >= 3; }
                game.SelectRound(game.Save.round);
                for (var state = 0; state < 3; state++)
                {
                    pixels.Draw(game, state, 2);
                    File.WriteAllBytes("Logs/ChiikawaPreview/team2-state-" + state + ".png", pixels.Texture.EncodeToPNG());
                }
                var board = new Texture2D(768, 512, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
                var outputPixels = new Color32[768 * 512];
                for (var state = 0; state < 4; state++) for (var characterId = 0; characterId < 6; characterId++)
                {
                    var frame = state == 3 ? art.PortraitFrame(characterId) : art.Frame(characterId, state);
                    var factor = (float)frame.PixelExtent / frame.Extent;
                    var magnify = state == 3 ? 3 : 2;
                    var w = Math.Max(1, Mathf.RoundToInt(frame.Width * factor)) * magnify;
                    var h = Math.Max(1, Mathf.RoundToInt(frame.Height * factor)) * magnify;
                    var source = frame.PixelArt(w, h);
                    for (var y = 0; y < h; y++) for (var x = 0; x < w; x++)
                        outputPixels[((3 - state) * 128 + y + (128 - h) / 2) * 768 + characterId * 128 + x + (128 - w) / 2] = source[y * w + x];
                }
                board.SetPixels32(outputPixels); board.Apply();
                File.WriteAllBytes("Logs/ChiikawaPreview/characters-v2.png", board.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(board);
            }

            var prefab = AssetDatabase.LoadAssetAtPath<DOTORIONView>("Assets/02. Prefabs/DOTORIONCanvas.prefab");
            var view = UnityEngine.Object.Instantiate(prefab);
            var host = new GameObject("PreviewWindow");
            var window = host.AddComponent<WindowsOverlayWindow>();
            var id = "preview-" + Guid.NewGuid().ToString("N");
            var now = DateTimeOffset.UtcNow;
            var catalog = AssetDatabase.LoadAssetAtPath<TeamAvatarCatalog>(DOTORIONPrefabBuilder.AvatarCatalogPath);
            view.SetAvatarCatalog(catalog);
            var key = catalog.Count > 0 ? catalog.Options[0].Key : "";
            var local = new MemberState(id, "요한", key, 0, AttendanceStatus.ClockedIn,
                ActivityStatus.Working, ConnectionStatus.Connected, now, now, now, null, now);
            var members = new MemberState[4]; members[0] = local;
            var names = new[] { "요한", "햄돌", "해피", "도치타임" };
            for (var i = 1; i < 4; i++) members[i] = new MemberState("preview-teammate-" + i, names[i],
                catalog.Count > i ? catalog.Options[i].Key : "", i, AttendanceStatus.ClockedIn,
                i == 1 ? ActivityStatus.Break : i == 2 ? ActivityStatus.Meal : ActivityStatus.Working,
                ConnectionStatus.Connected, now, now, now, null, now);
            view.Initialize(() => { });
            view.Bind(members, id, now, new UnreadNoteTracker());
            var module = view.gameObject.AddComponent<SubjugationEvent>();
            module.Initialize(view, window, id, () => local, () => true, () => { }, saveDirectory: "Logs/ChiikawaPreview/Saves");
            var field = typeof(SubjugationEvent).GetField("_game", BindingFlags.NonPublic | BindingFlags.Instance);
            var previewGame = (SubjugationGame)field.GetValue(module);
            previewGame.Save.coins = 9040; previewGame.Save.maxRound = 22;
            previewGame.Hire(1); previewGame.Hire(2);
            var rounds = (System.Collections.Generic.Dictionary<string, int>)typeof(SubjugationEvent).GetField("_rounds", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(module);
            rounds[members[1].MemberId] = 96; rounds[members[2].MemberId] = 100; rounds[members[3].MemberId] = 42;
            typeof(SubjugationEvent).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(module, null);
            typeof(SubjugationEvent).GetMethod("SetShop", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(module, new object[] { true });
            var canvas = view.GetComponent<Canvas>();
            var cameraObject = new GameObject("PreviewCamera");
            var camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.91f, .91f, .89f);
            canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1;
            var rt = RenderTexture.GetTemporary(960, 1244, 24);
            camera.targetTexture = rt;
            var bg = (RectTransform)view.EventPanelParent;
            view.SetEventStripInset(64);
            Canvas.ForceUpdateCanvases();
            typeof(SubjugationEvent).GetField("_nextFrame", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(module, -1f);
            typeof(SubjugationEvent).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(module, null);
            camera.Render();
            var old = RenderTexture.active; RenderTexture.active = rt;
            var output = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            output.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0); output.Apply();
            File.WriteAllBytes("Logs/ChiikawaPreview/roster.png", output.EncodeToPNG());
                        foreach (var result in new[] { 1, -1 })
            {
                typeof(SubjugationGame).GetProperty("Outcome").SetValue(previewGame, result);
                typeof(SubjugationEvent).GetField("_nextFrame", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(module, -1f);
                typeof(SubjugationEvent).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(module, null);
                Canvas.ForceUpdateCanvases(); camera.Render();
                RenderTexture.active = rt;
                output.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0); output.Apply();
                File.WriteAllBytes("Logs/ChiikawaPreview/integrated-" + (result == 1 ? "success" : "failure") + ".png", output.EncodeToPNG());
            }
            typeof(SubjugationGame).GetProperty("Outcome").SetValue(previewGame, 0);
            var eventPixels = (SubjugationPixels)typeof(SubjugationEvent).GetField("_pixels", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(module);
            var battle = new SubjugationGame();
            for (var i = 0; i < 4; i++) battle.Tick(1, true);
            eventPixels.Draw(battle, 0, 4);
            File.WriteAllBytes("Logs/ChiikawaPreview/success.png", eventPixels.Texture.EncodeToPNG());
            battle = new SubjugationGame(new SubjugationSave { round = 100 }); battle.Tick(1, true);
            eventPixels.Draw(battle, 0, 4);
            File.WriteAllBytes("Logs/ChiikawaPreview/failure.png", eventPixels.Texture.EncodeToPNG());
            battle = new SubjugationGame(); battle.Tick(1, true);
            eventPixels.Draw(battle, 0, 4);
            File.WriteAllBytes("Logs/ChiikawaPreview/damage.png", eventPixels.Texture.EncodeToPNG());
            for (var phase = 0; phase < 4; phase++)
            {
                var pose = new SubjugationGame(new SubjugationSave { coins = 10000 }); pose.Hire(1); pose.Hire(2);
                pose.Tick(phase * .25, true);
                eventPixels.Draw(pose, 0, 4);
                File.WriteAllBytes("Logs/ChiikawaPreview/attack-" + phase + ".png", eventPixels.Texture.EncodeToPNG());
            }
            var fallen = new SubjugationGame(new SubjugationSave { coins = 10000, round = 100 }); fallen.Hire(1); fallen.Hire(2); fallen.Tick(1, true);
            eventPixels.Draw(fallen, 0, 2);
            File.WriteAllBytes("Logs/ChiikawaPreview/front-fallen.png", eventPixels.Texture.EncodeToPNG());
            using (var monsterArt = new SubjugationArt())
            {
                var board = new Texture2D(640, 256, TextureFormat.RGBA32, false); var colors = new Color32[640 * 256];
                for (var enemyIndex = 0; enemyIndex < 10; enemyIndex++)
                {
                    var frame = monsterArt.Monster(enemyIndex + 1);
                    var zoom = 96 / SubjugationArt.CombatPixelExtent;
                    var w = frame.MasterWidth * zoom; var h = frame.MasterHeight * zoom; var source = frame.PixelArt(w, h);
                    for (var y = 0; y < h; y++) for (var x = 0; x < w; x++) colors[((1 - enemyIndex / 5) * 128 + 8 + y) * 640 + enemyIndex % 5 * 128 + (128 - w) / 2 + x] = source[y * w + x];
                }
                board.SetPixels32(colors); board.Apply(); File.WriteAllBytes("Logs/ChiikawaPreview/enemies-10.png", board.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(board);
            }
            previewGame.ToggleAutoAdvance();
            typeof(SubjugationEvent).GetMethod("ToggleRounds", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(module, null);
            eventPixels.AutoHovered = true; eventPixels.Draw(previewGame, 0, 2);
            Canvas.ForceUpdateCanvases(); camera.Render();
            RenderTexture.active = rt;
            output.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0); output.Apply();
            File.WriteAllBytes("Logs/ChiikawaPreview/round-picker.png", output.EncodeToPNG());
            RenderTexture.active = old; camera.targetTexture = null; RenderTexture.ReleaseTemporary(rt);
            UnityEngine.Object.DestroyImmediate(view.gameObject);
            UnityEngine.Object.DestroyImmediate(host);
            UnityEngine.Object.DestroyImmediate(cameraObject);
            UnityEngine.Object.DestroyImmediate(output);
            Debug.Log("Chiikawa visual preview exported: Logs/ChiikawaPreview");
        }
    }
}

