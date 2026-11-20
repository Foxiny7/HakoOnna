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
            public int hiderObjects, rolls, sector;
            public bool hiderOwner, transferChecks;
            public bool keptFull, swapped, contested;
            public int occupied, groundCount, held;
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
        private bool hiderCheck, blockedTransfer, cooldownHeld;
        private int hiderStep, initialBox, expectedSector = -1;
        private int[] nearby;
        private bool groundCheck, keptFull, swapped, contested;
        private int groundStep;
        private ulong raceId;
        private Vector3 raceDirection;
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
            check.hiderCheck = System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-hakoHiderSmoke") >= 0;
            check.groundCheck = System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-hakoGroundSmoke") >= 0;
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
            if (hiderCheck)
            {
                CheckHider(manager, players);
                return;
            }
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
            if (groundCheck)
            {
                CheckGround(manager, players, local, elapsed);
                return;
            }
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
                    var frame = door.WorldOpening;
                    var outward = frame.size.z < frame.size.x ? Vector3.forward : Vector3.right;
                    var foot = new Vector3(frame.center.x, frame.min.y + .05f, frame.center.z);
                    var character = searcher.GetComponent<CharacterController>();
                    character.enabled = false;
                    searcher.transform.position = manager.IsHost ? foot + outward : foot - outward;
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

        private void CheckHider(NetworkManager manager, NetworkPlayer[] players)
        {
            var hider = FindFirstObjectByType<NetworkHider>();
            bool ready = manager && manager.IsListening && players.Length == 1 &&
                (!manager.IsServer || manager.ConnectedClientsList.Count == 2) &&
                (!hider || hider.Current >= 0);
            if (!ready)
            {
                if (Time.unscaledTime > 50) WriteReport(manager, players);
                return;
            }
            if (allConnectedAt < 0) allConnectedAt = Time.unscaledTime;
            float elapsed = Time.unscaledTime - allConnectedAt;
            if (hider)
            {
                var session = FindFirstObjectByType<LocalSession>();
                var explorer = players[0];
                if (nearby == null)
                {
                    initialBox = hider.Current;
                    nearby = Enumerable.Range(0, session.Containers.Length).Where(i => i != initialBox)
                        .OrderBy(i => (session.Containers[i].transform.position - explorer.transform.position).sqrMagnitude)
                        .Take(3).ToArray();
                    if (manager.IsServer)
                        session.Rules.ReserveSearch((int)explorer.OwnerClientId, nearby[0], HakoOnna.Rules.SearchKind.Normal);
                }
                if (hider.IsOwner)
                {
                    if (hiderStep == 0 && elapsed > 2)
                    {
                        hider.Transfer(nearby[0]);
                        hiderStep++;
                    }
                    else if (hiderStep == 1 && elapsed > 4)
                    {
                        blockedTransfer = hider.Current == initialBox;
                        hider.Transfer(nearby[1]);
                        hider.Transfer(nearby[2]);
                        hiderStep++;
                    }
                    else if (hiderStep == 2 && elapsed > 8)
                    {
                        cooldownHeld = hider.Current == nearby[1];
                        hider.Transfer(nearby[2]);
                        hiderStep++;
                    }
                }
                if (manager.IsServer && hider.Current == nearby[2])
                {
                    var delta = session.Containers[hider.Current].transform.position - explorer.transform.position;
                    expectedSector = (Mathf.RoundToInt(Mathf.Atan2(delta.x, delta.z) * Mathf.Rad2Deg / 45) + 8) % 8;
                }
            }
            if (elapsed > 12) WriteReport(manager, players);
        }

        private void CheckGround(NetworkManager manager, NetworkPlayer[] players, NetworkPlayer local, float elapsed)
        {
            var first = players.OrderBy(p => p.OwnerClientId).First();
            if (manager.IsServer && !positioned)
            {
                var second = players.First(p => p != first);
                var controller = second.GetComponent<CharacterController>();
                controller.enabled = false;
                second.transform.position = first.transform.position + Vector3.right * .85f;
                controller.enabled = true;
                first.SeedExchangeTest(true);
                second.SeedExchangeTest(false);
                positioned = true;
            }
            if (elapsed > 16) { WriteReport(manager, players); return; }
            if (!local) return;
            bool fullPlayer = local == first;
            var items = FindObjectsByType<GroundItem>(FindObjectsSortMode.None);
            var key = items.FirstOrDefault(i => i.Kind.Value == HakoOnna.Rules.ItemKind.GateKey);
            var diary = items.FirstOrDefault(i => i.Kind.Value == HakoOnna.Rules.ItemKind.Diary);
            var eye = local.transform.position + Vector3.up * local.Config.EyeHeight;
            if (groundStep == 0 && elapsed > 2)
            {
                if (!fullPlayer) local.DropGroundRpc();
            }
            else if (groundStep == 1 && elapsed > 3 && key)
            {
                if (fullPlayer) local.TakeGroundRpc(key.NetworkObjectId, (key.transform.position - eye).normalized);
            }
            else if (groundStep == 2 && elapsed > 5 && key)
            {
                keptFull = !fullPlayer || local.TestOccupied == 9;
                if (fullPlayer) local.DropGroundRpc();
            }
            else if (groundStep == 3 && elapsed > 7 && (fullPlayer ? key : diary))
            {
                var target = fullPlayer ? key : diary;
                local.TakeGroundRpc(target.NetworkObjectId, (target.transform.position - eye).normalized);
            }
            else if (groundStep == 4 && elapsed > 9)
            {
                swapped = local.TestHeld == (int)(fullPlayer ? HakoOnna.Rules.ItemKind.GateKey : HakoOnna.Rules.ItemKind.Diary);
                local.DropGroundRpc();
            }
            else if (groundStep == 5 && elapsed > 11 && diary)
            {
                raceId = diary.NetworkObjectId;
                raceDirection = (diary.transform.position - eye).normalized;
            }
            else if (groundStep == 6 && elapsed > 13)
            {
                local.TakeGroundRpc(raceId, raceDirection);
                contested = true;
            }
            else return;
            groundStep++;
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
            if (hiderCheck)
            {
                var hider = FindFirstObjectByType<NetworkHider>();
                report.hiderObjects = hider ? 1 : 0;
                report.hiderOwner = hider && hider.IsOwner;
                report.transferChecks = report.hiderOwner && nearby != null && blockedTransfer && cooldownHeld &&
                    hider.Current == nearby[2];
                report.sector = expectedSector;
                if (local && local.TryGetComponent<NetworkPlayer>(out var listener))
                {
                    report.rolls = listener.ReceivedRolls;
                    report.sector = listener.LastRollSector;
                }
            }
            if (groundCheck)
            {
                report.keptFull = keptFull;
                report.swapped = swapped;
                report.contested = contested;
                report.groundCount = FindObjectsByType<GroundItem>(FindObjectsSortMode.None).Length;
                if (local)
                {
                    var owner = local.GetComponent<NetworkPlayer>();
                    report.held = owner.TestHeld;
                    report.occupied = owner.TestOccupied;
                }
            }
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
