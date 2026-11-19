#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections.Generic;
using System.IO;
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

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Initialize()
        {
            var path = LocalSession.Argument("-hakoSmoke");
            if (string.IsNullOrEmpty(path))
                return;

            var check = new GameObject("LocalNetworkSmokeCheck").AddComponent<LocalNetworkSmokeCheck>();
            check.reportPath = path;
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
                local.SupplyTestInput(direction, 0, false);
            }
            if (elapsed >= 7)
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
                errors = errors
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
