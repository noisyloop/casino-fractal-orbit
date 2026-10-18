using System.IO;
using InfiniteOrbit.GameMath;
using InfiniteOrbit.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace InfiniteOrbit.EditorTools
{
    /// <summary>Editor conveniences: scene setup and a quick in-editor RTP check using the shared math module.</summary>
    public static class InfiniteOrbitMenu
    {
        private const string ScenePath = "Assets/InfiniteOrbit/Scenes/Main.unity";

        [MenuItem("Infinite Orbit/Create Main Scene")]
        public static void CreateMainScene()
        {
            Directory.CreateDirectory("Assets/InfiniteOrbit/Scenes");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            new GameObject("Infinite Orbit").AddComponent<GameDirector>();
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            Debug.Log("Infinite Orbit: created " + ScenePath + " and added it to Build Settings.");
        }

        [MenuItem("Infinite Orbit/Simulate 1M Spins")]
        public static void Simulate()
        {
            var stats = Simulation.Run(InfiniteOrbitMath.CreateConfig(), 20261018UL, 1_000_000);
            Debug.Log($"Infinite Orbit 1M spins: RTP {stats.Rtp:P3} (±{stats.Rtp95HalfWidth:P3}), hit {stats.HitFrequency:P2}, " +
                      $"FS 1 in {(double)stats.Rounds / stats.FreeSpinTriggers:F1}, HS 1 in {(double)stats.Rounds / stats.HoldAndSpinTriggers:F1}, max {stats.MaxWinMultiple:F1}x. " +
                      "Use Tools/InfiniteOrbit.Simulator for 10M+ runs and the PAR sheet.");
        }
    }
}
