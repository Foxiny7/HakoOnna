using System;
using System.Collections;
using System.Collections.Generic;
using HakoOnna.Art;
using HakoOnna.Rules;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HakoOnna.Multiplayer
{
    [RequireComponent(typeof(NetworkManager), typeof(UnityTransport))]
    public sealed class LocalSession : MonoBehaviour
    {
        public const string HouseScene = "Assets/_Art/Scenes/House_Blockout.unity";
        public const ushort Port = 7777;

        [SerializeField, Tooltip("Use gray ambient lighting for movement tests when starting the scene.")]
        private bool debugAmbientFill;

        private readonly Dictionary<ulong, int> spawnSlots = new Dictionary<ulong, int>();
        private NetworkManager manager;
        private Transform[] spawnPoints;
        private Camera previewCamera;
        private ulong? hiderClient;
        private bool playHider;
        internal int TestBox { get; private set; } = -1;
        internal bool CanReplayGrab { get; set; }
        internal GameConfig Config => manager.NetworkConfig.PlayerPrefab.GetComponent<NetworkPlayer>().Config;
        private bool ready;
        private string status = "Loading house...";

        public bool Ready => ready;
        internal ContainerVisual[] Containers { get; private set; }
        internal GameObject GroundPrefab { get; private set; }
        internal HiderRoutes Routes { get; private set; }
        internal DoorVisual[] Doors { get; private set; }
        internal MatchRules Rules { get; private set; }

        private IEnumerator Start()
        {
            Application.runInBackground = true;
            Application.targetFrameRate = 60;
            manager = GetComponent<NetworkManager>();
            GroundPrefab = Resources.Load<GameObject>("GroundItem");
            manager.ConnectionApprovalCallback = ApproveConnection;
            manager.OnClientConnectedCallback += OnConnected;
            manager.OnClientDisconnectCallback += OnDisconnected;
            manager.OnTransportFailure += OnTransportFailure;
            manager.OnServerStopped += OnServerStopped;
            GetComponent<UnityTransport>().SetConnectionData("127.0.0.1", Port, "127.0.0.1");

            yield return SceneManager.LoadSceneAsync(HouseScene, LoadSceneMode.Additive);
            var house = SceneManager.GetSceneByPath(HouseScene);
            SceneManager.SetActiveScene(house);
            Containers = GameObject.Find("House_Blockout/Containers").GetComponentsInChildren<ContainerVisual>();
            Routes = new HiderRoutes(Containers);
            Doors = GameObject.Find("House_Blockout").GetComponentsInChildren<DoorVisual>();
            foreach (var container in Containers)
                ConfigureDrawer(container);
            ZashikiArt.DressAlcove(GameObject.Find("House_Blockout/Props/Zashiki_Tokonoma").transform);
            if (debugAmbientFill)
            {
                RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
                RenderSettings.ambientLight = Color.gray;
            }
            var ambient = new UnityEngine.Rendering.SphericalHarmonicsL2();
            ambient.AddAmbientLight(RenderSettings.ambientLight);
            RenderSettings.ambientProbe = ambient;
            var spawns = GameObject.Find("House_Blockout/Gameplay/ExplorerSpawnPoints").transform;
            spawnPoints = new Transform[spawns.childCount];
            for (var index = 0; index < spawnPoints.Length; index++)
                spawnPoints[index] = spawns.GetChild(index);

            previewCamera = GameObject.Find("House_Blockout/Lighting/BlockoutPreviewCamera_DeleteLater").GetComponent<Camera>();
            previewCamera.gameObject.SetActive(false);
            ready = true;
            status = "Local server 127.0.0.1:7777";
            playHider = Argument("-hakoRole") == "hider";
            var mode = Argument("-hakoMode");
            if (!string.IsNullOrEmpty(mode))
                StartSession(mode);
        }

        internal static void ConfigureDrawer(ContainerVisual visual)
        {
            var top = visual.transform.Find("Drawer_0");
            var bottom = visual.transform.Find("Drawer_2");
            if (!top || !bottom || !top.Find("ItemAnchor")) return;

            var closed = top.localPosition;
            visual.SetOpenImmediate(true);
            var travel = top.localPosition - closed;
            visual.SetOpenImmediate(false);
            for (int i = top.childCount - 1; i >= 0; i--)
            {
                var child = top.GetChild(i);
                if (child.name == "ItemAnchor" || child.name.StartsWith("Tray"))
                    child.SetParent(bottom, false);
            }
            visual.ConfigureParts(new[] { new ContainerVisual.MovingPart { part = bottom, openOffset = travel } });
            if (visual.transform.parent.name == "Zashiki_Tansu_02")
                ZashikiArt.DressChest(visual.transform, bottom);
            // The guest futon occupies the right side of this extended tray in the current house.
            if (visual.transform.parent.name == "Oku_Tansu_03")
            {
                var anchor = bottom.Find("ItemAnchor");
                anchor.localPosition += Vector3.left * (bottom.Find("TrayBottom").localScale.x * .30f);
            }
        }

        public void StartSession(string mode)
        {
            if (!ready || manager.IsListening || manager.ShutdownInProgress)
                return;

            CanReplayGrab = false;
            TestBox = mode == "occupied" ? Array.FindIndex(Containers, c => c.transform.parent.name == "Zashiki_Tansu_02") : -1;
            if (TestBox >= 0) { mode = "host"; playHider = false; }
            spawnSlots.Clear();
            hiderClient = null;
            manager.NetworkConfig.ConnectionData = new byte[] { (byte)(playHider ? 1 : 0) };
            status = mode + " starting...";
            foreach (var door in Doors) door.SetOpenImmediate(door.StartsOpen);
            if (mode == "host" || mode == "server")
            {
                var config = manager.NetworkConfig.PlayerPrefab.GetComponent<NetworkPlayer>().Config;
                Rules = new MatchRules(config.ToRulesSettings(), ItemKind.Gasoline);
                for (var i = 0; i < Containers.Length; i++)
                    Rules.AddContainer(i);
                if (TestBox >= 0) Rules.PlaceHakoOnna(-1, TestBox);
            }
            bool started;
            switch (mode)
            {
                case "host": started = manager.StartHost(); break;
                case "server": started = manager.StartServer(); break;
                case "client": started = manager.StartClient(); break;
                default: throw new ArgumentException("Use host, server or client.", nameof(mode));
            }
            if (!started)
                status = "Could not start " + mode;
            else if (mode == "server")
                status = "Local server running";
        }

        internal IEnumerator PlayOccupied(int id, Transform victim)
        {
            CanReplayGrab = false;
            yield return OccupiedOpening.Play(Containers[id], victim);
            CanReplayGrab = id == TestBox;
        }

        public void Disconnect()
        {
            manager.Shutdown();
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            status = "Disconnected";
        }

        private void ApproveConnection(NetworkManager.ConnectionApprovalRequest request,
            NetworkManager.ConnectionApprovalResponse response)
        {
            if (TestBox >= 0 && request.ClientNetworkId != NetworkManager.ServerClientId)
            {
                response.Approved = false;
                response.Reason = "Solo animation test.";
                return;
            }
            if (request.Payload is { Length: 1 } && request.Payload[0] == 1)
            {
                response.Approved = !hiderClient.HasValue;
                response.CreatePlayerObject = false;
                response.Reason = response.Approved ? "" : "Hako Onna is already taken.";
                if (response.Approved) hiderClient = request.ClientNetworkId;
                return;
            }
            var slot = 0;
            while (slot < spawnPoints.Length && spawnSlots.ContainsValue(slot))
                slot++;

            response.Approved = slot < spawnPoints.Length;
            response.CreatePlayerObject = response.Approved;
            response.Reason = response.Approved ? "" : "All five graybox spawn points are occupied.";
            if (!response.Approved)
                return;

            spawnSlots.Add(request.ClientNetworkId, slot);
            response.Position = spawnPoints[slot].position + Vector3.up * 0.05f;
            response.Rotation = spawnPoints[slot].rotation;
            if (TestBox >= 0)
            {
                var marker = Containers[TestBox].transform.parent.Find("InteractPoint");
                response.Position = marker.position + marker.forward * .22f + Vector3.up * .05f;
                response.Rotation = Quaternion.LookRotation(-marker.forward);
            }
        }

        private void OnConnected(ulong clientId)
        {
            if (manager.IsServer && hiderClient == clientId && !manager.ConnectedClients[clientId].PlayerObject)
            {
                var pawn = Instantiate(Resources.Load<GameObject>("HakoOnnaPlayer")).GetComponent<NetworkObject>();
                pawn.CheckObjectVisibility = observer => observer == clientId;
                pawn.SpawnAsPlayerObject(clientId);
            }
            status = manager.IsHost ? "Host connected" : manager.IsServer ? "Local server running" : "Connected";
        }

        private void OnDisconnected(ulong clientId)
        {
            spawnSlots.Remove(clientId);
            if (clientId == manager.LocalClientId)
            {
                status = string.IsNullOrEmpty(manager.DisconnectReason) ? "Disconnected" : manager.DisconnectReason;
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }

        private void OnTransportFailure()
        {
            status = "Transport failed. Check that another local server is not using port 7777.";
        }

        private void OnServerStopped(bool wasHost)
        {
            spawnSlots.Clear();
            status = "Server stopped";
        }

        private void OnGUI()
        {
            if (Application.isBatchMode)
                return;

            GUILayout.BeginArea(new Rect(16, 16, 380, 250), GUI.skin.box);
            GUILayout.Label(status);
            if (manager != null && manager.IsListening)
            {
                GUILayout.Label("Explorers visible: " + FindObjectsByType<NetworkPlayer>(FindObjectsSortMode.None).Length);
                if (manager.LocalClient?.PlayerObject && manager.LocalClient.PlayerObject.GetComponent<NetworkHider>())
                    GUILayout.Label("Hako Onna  |  Click a highlighted container, then E");
                else
                {
                    GUILayout.Label("WASD move | Shift run | Space jump | Esc release cursor");
                    GUILayout.Label("1–9 / Wheel: Equip   |   T: Flashlight   |   G: Drop");
                    if (Cursor.lockState == CursorLockMode.None && GUILayout.Button("Capture mouse"))
                    {
                        Cursor.lockState = CursorLockMode.Locked;
                        Cursor.visible = false;
                    }
                }
                if (TestBox >= 0)
                {
                    GUILayout.Label("Occupied box test   |   Aim at the drawer and hold E");
                    if (CanReplayGrab && GUILayout.Button("Replay occupied opening"))
                        StartCoroutine(PlayOccupied(TestBox, manager.LocalClient.PlayerObject.transform));
                }
                if (GUILayout.Button("Disconnect"))
                    Disconnect();
            }
            else
            {
                GUI.enabled = ready && manager != null && !manager.ShutdownInProgress;
                playHider = GUILayout.Toggle(playHider, "Play as Hako Onna (one per house)");
                if (GUILayout.Button("Test occupied container (solo)"))
                    StartSession("occupied");
                if (GUILayout.Button("Host on this PC"))
                    StartSession("host");
                if (GUILayout.Button("Join 127.0.0.1"))
                    StartSession("client");
                if (GUILayout.Button("Start local server"))
                    StartSession("server");
                GUI.enabled = true;
            }
            GUILayout.EndArea();
        }

        private void OnDestroy()
        {
            if (manager == null)
                return;
            manager.ConnectionApprovalCallback = null;
            manager.OnClientConnectedCallback -= OnConnected;
            manager.OnClientDisconnectCallback -= OnDisconnected;
            manager.OnTransportFailure -= OnTransportFailure;
            manager.OnServerStopped -= OnServerStopped;
        }

        public static string Argument(string name)
        {
            var args = Environment.GetCommandLineArgs();
            var index = Array.IndexOf(args, name);
            return index >= 0 && index + 1 < args.Length ? args[index + 1] : "";
        }
    }
}
