using System;
using System.IO;
using HakoOnna.Multiplayer;
using Unity.Netcode;
using Unity.Netcode.Components;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace HakoOnna.EditorTools
{
    public static class PhaseOneSetup
    {
        public const string ScenePath = "Assets/_Game/Scenes/LocalMultiplayer.unity";
        public const string BuildPath = "Builds/PhaseOne/HakoOnna.exe";
        private const string PrefabPath = "Assets/_Game/Prefabs/Explorer.prefab";
        private static System.Diagnostics.Process localServer;

        [MenuItem("Hako/Playtest/1 Create local multiplayer scene")]
        public static void CreateScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;
            EnsureBuildScenes();
            Directory.CreateDirectory("Assets/_Game/Prefabs");
            var config = AssetDatabase.LoadAssetAtPath<GameConfig>("Assets/_Game/Config/GameConfig.asset");
            var prefab = CreatePlayerPrefab(config);
            if (File.Exists(ScenePath))
            {
                EditorSceneManager.OpenScene(ScenePath);
                return;
            }

            Directory.CreateDirectory("Assets/_Game/Scenes");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject("LocalSession");
            var transport = root.AddComponent<UnityTransport>();
            var manager = root.AddComponent<NetworkManager>();
            manager.NetworkConfig.NetworkTransport = transport;
            manager.NetworkConfig.EnableSceneManagement = false;
            manager.NetworkConfig.ConnectionApproval = true;
            manager.NetworkConfig.TickRate = 30;
            manager.NetworkConfig.PlayerPrefab = prefab;
            root.AddComponent<LocalSession>();
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
        }

        public static void CreateGroundItemPrefab()
        {
            const string folder = "Assets/_Game/Resources";
            const string path = folder + "/GroundItem.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path)) return;
            if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder("Assets/_Game", "Resources");
            var root = new GameObject("GroundItem");
            root.layer = LayerMask.NameToLayer("Ignore Raycast");
            var target = root.AddComponent<BoxCollider>();
            target.isTrigger = true;
            target.size = new Vector3(.28f, .16f, .30f);
            root.AddComponent<NetworkObject>();
            root.AddComponent<GroundItem>();
            foreach (var name in new[] { "Flashlight", "Parcel" })
            {
                bool lamp = name == "Flashlight";
                var mesh = GameObject.CreatePrimitive(lamp ? PrimitiveType.Cylinder : PrimitiveType.Cube);
                mesh.name = name;
                mesh.transform.SetParent(root.transform, false);
                UnityEngine.Object.DestroyImmediate(mesh.GetComponent<Collider>());
                mesh.transform.localScale = lamp ? new Vector3(.09f, .13f, .09f) : new Vector3(.22f, .08f, .16f);
                if (lamp) mesh.transform.localRotation = Quaternion.Euler(90, 0, 0);
            }
            PrefabUtility.SaveAsPrefabAsset(root, path);
            UnityEngine.Object.DestroyImmediate(root);
            AssetDatabase.SaveAssets();
        }

        public static void CreateHiderPrefab()
        {
            const string path = "Assets/_Game/Resources/HakoOnnaPlayer.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path)) return;
            var root = new GameObject("HakoOnnaPlayer");
            root.AddComponent<NetworkObject>().SynchronizeTransform = false;
            root.AddComponent<NetworkHider>();
            var cylinder = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            cylinder.name = "Body";
            cylinder.transform.SetParent(root.transform, false);
            UnityEngine.Object.DestroyImmediate(cylinder.GetComponent<Collider>());
            var config = AssetDatabase.LoadAssetAtPath<GameConfig>("Assets/_Game/Config/GameConfig.asset");
            cylinder.transform.localScale = new Vector3(config.PlayerRadius * 2, config.PlayerHeight / 2, config.PlayerRadius * 2);
            var red = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = new Color(.78f, .035f, .055f) };
            AssetDatabase.CreateAsset(red, "Assets/_Game/Prefabs/HakoOnnaRed.mat");
            cylinder.GetComponent<Renderer>().sharedMaterial = red;
            cylinder.SetActive(false);
            var view = new GameObject("HiderView", typeof(Camera), typeof(AudioListener));
            view.transform.SetParent(root.transform, false);
            var camera = view.GetComponent<Camera>();
            camera.nearClipPlane = .04f;
            camera.fieldOfView = 75;
            camera.GetUniversalAdditionalCameraData().renderPostProcessing = true;
            view.SetActive(false);
            PrefabUtility.SaveAsPrefabAsset(root, path);
            UnityEngine.Object.DestroyImmediate(root);
            AssetDatabase.SaveAssets();
        }

        private static void EnsureBuildScenes()
        {
            PlayerSettings.runInBackground = true;
            var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene>
            {
                new EditorBuildSettingsScene(ScenePath, true),
                new EditorBuildSettingsScene(LocalSession.HouseScene, true)
            };
            foreach (var scene in EditorBuildSettings.scenes)
            {
                if (scene.path != ScenePath && scene.path != LocalSession.HouseScene)
                    scenes.Add(scene);
            }
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        private static GameObject CreatePlayerPrefab(GameConfig config)
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (existing != null)
            {
                var contents = PrefabUtility.LoadPrefabContents(PrefabPath);
                try
                {
                    contents.GetComponent<NetworkPlayer>().ConfigureDimensions();
                    PrefabUtility.SaveAsPrefabAsset(contents, PrefabPath);
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(contents);
                }
                return existing;
            }

            var player = new GameObject("Explorer");
            var controller = player.AddComponent<CharacterController>();
            controller.slopeLimit = 50f;
            controller.enabled = false;
            player.AddComponent<NetworkObject>();
            var sync = player.AddComponent<NetworkTransform>();
            sync.Interpolate = true;
            sync.SyncRotAngleX = false;
            sync.SyncRotAngleZ = false;
            sync.SyncScaleX = false;
            sync.SyncScaleY = false;
            sync.SyncScaleZ = false;
            var logic = player.AddComponent<NetworkPlayer>();

            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Body";
            body.transform.SetParent(player.transform, false);
            UnityEngine.Object.DestroyImmediate(body.GetComponent<Collider>());
            var cameraObject = new GameObject("PlayerCamera", typeof(Camera), typeof(AudioListener));
            cameraObject.transform.SetParent(player.transform, false);
            var camera = cameraObject.GetComponent<Camera>();
            camera.nearClipPlane = 0.05f;
            camera.fieldOfView = 75f;
            camera.GetUniversalAdditionalCameraData().renderPostProcessing = true;
            var arms = new GameObject("Arms");
            arms.transform.SetParent(cameraObject.transform, false);
            foreach (var side in new[] { -1f, 1f })
            {
                var arm = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                arm.name = side < 0 ? "LeftArm" : "RightArm";
                arm.transform.SetParent(arms.transform, false);
                arm.transform.localPosition = new Vector3(side * 0.27f, -0.32f, 0.35f);
                arm.transform.localScale = new Vector3(0.1f, 0.23f, 0.1f);
                arm.transform.localRotation = Quaternion.Euler(70f, 0, side * -10f);
                UnityEngine.Object.DestroyImmediate(arm.GetComponent<Collider>());
            }
            cameraObject.SetActive(false);
            var serialized = new SerializedObject(logic);
            serialized.FindProperty("config").objectReferenceValue = config;
            serialized.FindProperty("playerCamera").objectReferenceValue = camera;
            serialized.FindProperty("body").objectReferenceValue = body.GetComponent<Renderer>();
            serialized.FindProperty("arms").objectReferenceValue = arms;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            logic.ConfigureDimensions();
            var result = PrefabUtility.SaveAsPrefabAsset(player, PrefabPath);
            UnityEngine.Object.DestroyImmediate(player);
            return result;
        }

        [MenuItem("Hako/Playtest/2 Build Windows player")]
        public static void Build()
        {
            if (!File.Exists(ScenePath))
                throw new InvalidOperationException("Create the local multiplayer scene first.");

            var result = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath, LocalSession.HouseScene },
                locationPathName = BuildPath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development
            });
            if (result == null || result.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
                throw new InvalidOperationException("Phase 1 build failed.");
            Debug.Log("[Hako] Player built at " + Path.GetFullPath(BuildPath));
        }

        [MenuItem("Hako/Playtest/3 Start extra local client")]
        public static void StartClient()
        {
            StartPlayer("-hakoMode client -screen-fullscreen 0 -screen-width 960 -screen-height 600");
        }

        [MenuItem("Hako/Playtest/4 Start local dedicated server")]
        public static void StartServer()
        {
            if (localServer == null || localServer.HasExited)
                localServer = StartPlayer("-batchmode -nographics -hakoMode server");
        }

        [MenuItem("Hako/Playtest/5 Stop local dedicated server")]
        public static void StopServer()
        {
            if (localServer != null && !localServer.HasExited)
                localServer.Kill();
            localServer = null;
        }

        private static System.Diagnostics.Process StartPlayer(string arguments)
        {
            var executable = Path.GetFullPath(BuildPath);
            if (!File.Exists(executable))
                throw new InvalidOperationException("Build the Windows player first.");
            return System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(executable, arguments)
            {
                UseShellExecute = false,
                WorkingDirectory = Path.GetDirectoryName(executable),
                WindowStyle = arguments.StartsWith("-batchmode", StringComparison.Ordinal)
                    ? System.Diagnostics.ProcessWindowStyle.Hidden
                    : System.Diagnostics.ProcessWindowStyle.Normal
            });
        }
    }
}
