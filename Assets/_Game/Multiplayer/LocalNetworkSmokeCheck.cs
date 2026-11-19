#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.Netcode;
using UnityEngine;

namespace HakoOnna.Multiplayer
{
    // Development-build acceptance check. Activated explicitly with -hakoSmoke <report path>.
    [DefaultExecutionOrder(1000)]
    public sealed class LocalNetworkSmokeCheck : MonoBehaviour
    {
        [System.Serializable]
        private sealed class PlayerSnapshot
        {
            public ulong owner;
            public Vector3 position;
            public bool serverController;
        }

        [System.Serializable]
        private sealed class Report
        {
            public bool server;
            public bool client;
            public ulong localClient;
            public int peakPlayers;
            public float localDistance;
            public int activeCameras;
            public bool actor, sawOpen, sawClose, exchanged;
            public bool jumped, tired, recovered, doorClosed;
            public List<string> errors;
            public List<PlayerSnapshot> players = new List<PlayerSnapshot>();
        }

        private readonly List<string> errors = new List<string>();
        private string reportPath;
        private int expectedPlayers;
        private float allConnectedAt = -1;
        private Vector3 localStart;
        private bool recordedStart;
        private bool reported;
        private bool containerCheck, positioned, actor, sawOpen, sawClose, exchanged;
        private int searchStep;
        private bool jumped, tired, recovered, doorClosed, nearDoor, jumpSent;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Initialize()
        {
            var path = LocalSession.Argument("-hakoSmoke");
            if (string.IsNullOrEmpty(path))
                return;

            var check = new GameObject("LocalNetworkSmokeCheck").AddComponent<LocalNetworkSmokeCheck>();
            check.reportPath = path;
            check.containerCheck = System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-hakoContainerSmoke") >= 0;
            check.expectedPlayers = int.Parse(LocalSession.Argument("-hakoSmokePlayers"));
            DontDestroyOnLoad(check);
        }

        private void Awake()
        {
            Application.logMessageReceived += RecordError;
        }

        private void OnDestroy()
        {
            Application.logMessageReceived -= RecordError;
        }

        private void RecordError(string message, string trace, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
                errors.Add(message + "\n" + trace);
        }

        private void Update()
        {
            if (reported)
                return;
            var manager = NetworkManager.Singleton;
            var players = FindObjectsByType<NetworkPlayer>(FindObjectsSortMode.None);
            if (allConnectedAt < 0 && players.Length == expectedPlayers)
                allConnectedAt = Time.unscaledTime;

            if (Time.unscaledTime > 50 && allConnectedAt < 0)
            {
                WriteReport(manager, players);
                return;
            }
            if (allConnectedAt < 0)
                return;

            var elapsed = Time.unscaledTime - allConnectedAt;
            var local = manager.LocalClient?.PlayerObject?.GetComponent<NetworkPlayer>();
            if (containerCheck)
            {
                var containers = FindFirstObjectByType<LocalSession>().Containers;
                var drawerId = System.Array.FindIndex(containers, box => box.transform.parent.name == "Zashiki_Tansu_02");
                var visual = containers[drawerId];
                var searcher = players.OrderByDescending(player => player.OwnerClientId).First();
                actor = local == searcher;
                if (manager.IsServer && !positioned)
                {
                    var character = searcher.GetComponent<CharacterController>();
                    character.enabled = false;
                    var marker = visual.transform.parent.Find("InteractPoint");
                    searcher.transform.position = marker.position + marker.forward * .35f + marker.right * .25f + Vector3.up * .05f;
                    character.enabled = true;
                    positioned = true;
                }
                if (visual.IsOpen)
                {
                    var marker = visual.transform.parent.Find("InteractPoint");
                    var delta = Vector3.ProjectOnPlane(searcher.transform.position - marker.position, Vector3.up);
                    var bottom = visual.transform.Find("Drawer_2");
                    var top = visual.transform.Find("Drawer_0");
                    sawOpen |= delta.magnitude < .07f && bottom.localPosition.z > top.localPosition.z + .01f;
                }
                if (sawOpen && !visual.IsOpen) sawClose = true;
                if (actor && elapsed > 2)
                    searcher.CheckContainerExchange(visual, drawerId, ref searchStep, ref exchanged);
                var door = FindFirstObjectByType<LocalSession>().Doors.First(d => d.name == "Door_Outside_Genkan");
                if (manager.IsServer && sawClose && !nearDoor)
                {
                    var leaf = door.GetComponentInChildren<BoxCollider>().bounds;
                    var outward = (leaf.size.z < leaf.size.x ? Vector3.forward : Vector3.right);
                    var foot = new Vector3(leaf.center.x, leaf.min.y + .05f, leaf.center.z);
                    if (Physics.Linecast(foot + Vector3.up + outward, leaf.center, out var hit) &&
                        HakoOnna.Art.DoorVisual.FromCollider(hit.collider) != door) outward = -outward;
                    var character = searcher.GetComponent<CharacterController>();
                    character.enabled = false;
                    searcher.transform.position = foot + outward;
                    character.enabled = true;
                    nearDoor = true;
                }
                if (actor && exchanged && sawClose) searcher.TryTestDoor(door);
                doorClosed |= !door.IsOpen && !door.IsMoving;
                if (elapsed > 12) WriteReport(manager, players);
                return;
            }
            if (local != null)
            {
                if (!recordedStart)
                {
                    localStart = local.transform.position;
                    recordedStart = true;
                }
                // First move within the room, then keep walking into the west wall.
                // Both steps enter through normal owner input and server collision.
                var direction = elapsed >= 1 && elapsed < 2 ? Vector2.up :
                    elapsed >= 3 && elapsed < 5 ? Vector2.left : Vector2.zero;
                bool running = elapsed >= 7 && elapsed < 16;
                if (running)
                    direction = Mathf.Repeat(elapsed - 7, 1f) < .5f ? Vector2.right : Vector2.left;
                bool jump = elapsed > 1 && !jumpSent;
                jumpSent |= jump;
                local.SupplyTestInput(direction, 0, running, jump);
                jumped |= local.transform.position.y - localStart.y > .35f;
                tired |= local.IsExhausted;
                recovered |= tired && !local.IsExhausted;
            }
            if (elapsed >= 21)
                WriteReport(manager, players);
        }

        private void WriteReport(NetworkManager manager, NetworkPlayer[] players)
        {
            reported = true;
            var report = new Report
            {
                server = manager != null && manager.IsServer,
                client = manager != null && manager.IsClient,
                localClient = manager != null ? manager.LocalClientId : 0,
                peakPlayers = players.Length,
                activeCameras = FindObjectsByType<Camera>(FindObjectsSortMode.None).Length,
                errors = errors,
                actor = actor, sawOpen = sawOpen, sawClose = sawClose, exchanged = exchanged,
                jumped = jumped, tired = tired, recovered = recovered, doorClosed = doorClosed
            };
            var local = manager?.LocalClient?.PlayerObject;
            if (local != null)
                report.localDistance = Vector3.Distance(localStart, local.transform.position);

            foreach (var player in players)
            {
                report.players.Add(new PlayerSnapshot
                {
                    owner = player.OwnerClientId,
                    position = player.transform.position,
                    serverController = player.GetComponent<CharacterController>().enabled
                });
            }
            File.WriteAllText(reportPath, JsonUtility.ToJson(report, true));
            Debug.Log("[Hako smoke] report " + reportPath);
            // Keep the peer alive long enough for all observers to sample before disconnecting.
            Invoke(nameof(Finish), 5);
        }

        private void Finish()
        {
            Application.Quit(errors.Count == 0 ? 0 : 1);
        }
    }
}
#endif
