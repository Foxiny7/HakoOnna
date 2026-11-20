using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace HakoOnna.Multiplayer
{
    public sealed class NetworkHider : NetworkBehaviour
    {
        private readonly NetworkVariable<int> container = new(-1, NetworkVariableReadPermission.Owner);
        private readonly NetworkVariable<double> readyAt = new(0, NetworkVariableReadPermission.Owner);
        private LocalSession session;
        private Transform view, body;
        private GhostSight sight;
        private int selected = -1, hovered = -1;
        private float yaw, pitch = 16;
        private string feedback = "";
        internal int Current => container.Value;

        public override void OnNetworkSpawn()
        {
            session = FindFirstObjectByType<LocalSession>();
            view = transform.Find("HiderView");
            body = transform.Find("Body");
            if (IsServer)
            {
                int start = Random.Range(0, session.Containers.Length);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if (System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-hakoHiderSmoke") >= 0)
                    start = System.Array.FindIndex(session.Containers, c => c.transform.parent.name == "Genkan_GetaBako_01");
#endif
                while (!session.Rules.CanHideIn(start)) start = (start + 1) % session.Containers.Length;
                container.Value = start;
                session.Rules.PlaceHakoOnna(checked((int)OwnerClientId), Current);
            }
            if (!IsOwner) return;
            container.OnValueChanged += MoveView;
            MoveView(-1, Current);
            if (Application.isBatchMode) return;
            view.gameObject.SetActive(true);
            body.gameObject.SetActive(true);
            sight = new GhostSight(view.GetComponent<Camera>(), body, session);
            MoveView(-1, Current);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void MoveView(int previous, int next)
        {
            if (next < 0) return;
            var marker = session.Containers[next].transform.parent.Find("InteractPoint");
            var bounds = session.Containers[next].GetComponentInChildren<Collider>().bounds;
            selected = hovered = -1;
            body.position = bounds.center;
            var size = new Vector3(session.Config.PlayerRadius * 2, session.Config.PlayerHeight, session.Config.PlayerRadius * 2);
            float fit = .85f * Mathf.Min(bounds.size.x / size.x, bounds.size.y / size.y, bounds.size.z / size.z, 1f);
            body.localScale = new Vector3(size.x, size.y / 2, size.z) * fit;
            yaw = marker.eulerAngles.y + 180;
            pitch = 16;
            sight?.Show(next);
            Orbit();
            feedback = "";
        }

        internal void Transfer(int destination) => TransferRpc(destination);

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        private void TransferRpc(int destination)
        {
            session.Rules.AdvanceTo(Time.realtimeSinceStartupAsDouble);
            if (!session.Routes.Allows(Current, destination) || !session.Rules.TryTransfer(checked((int)OwnerClientId), destination))
            {
                DeniedRpc();
                return;
            }
            var origin = session.Containers[destination].transform.position;
            container.Value = destination;
            readyAt.Value = NetworkManager.ServerTime.Time + session.Config.ToRulesSettings().TransferCooldownSeconds;
            foreach (var peer in NetworkManager.ConnectedClientsList)
                if (peer.PlayerObject && peer.PlayerObject.TryGetComponent<NetworkPlayer>(out var explorer))
                    explorer.HearRoll(origin);
            OwnRollRpc();
        }

        [Rpc(SendTo.Owner)]
        private void OwnRollRpc() => AudioSource.PlayClipAtPoint(Resources.Load<AudioClip>("HiderRoll"), view.position);

        [Rpc(SendTo.Owner)]
        private void DeniedRpc() => feedback = "Transfer unavailable.";

        private void Orbit()
        {
            var rotation = Quaternion.Euler(pitch, yaw, 0);
            var eye = body.position;
            float distance = 2.6f;
            if (Physics.SphereCast(eye, .10f, rotation * Vector3.back, out var hit, distance,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                distance = Mathf.Clamp(hit.distance - .08f, .65f, distance);
            view.SetPositionAndRotation(eye - rotation * Vector3.forward * distance, rotation);
        }

        private void Update()
        {
            if (!IsOwner || !IsSpawned || Application.isBatchMode || !Application.isFocused) return;
            var mouse = Mouse.current;
            if (sight == null || mouse == null) return;
            if (mouse.rightButton.isPressed)
            {
                var delta = mouse.delta.ReadValue() * session.Config.MouseSensitivity;
                yaw += delta.x;
                pitch = Mathf.Clamp(pitch - delta.y, -75, 75);
                Orbit();
            }
            var pointer = mouse.position.ReadValue();
            hovered = sight.Pick(pointer, Current, selected, mouse.leftButton.wasPressedThisFrame);
            if (mouse.leftButton.wasPressedThisFrame && hovered >= 0)
            {
                selected = hovered;
                sight.Show(Current, selected);
                feedback = "";
            }
            if (selected >= 0 && Keyboard.current?.eKey.wasPressedThisFrame == true &&
                readyAt.Value <= NetworkManager.ServerTime.Time) Transfer(selected);
        }

        private void OnGUI()
        {
            if (!IsSpawned || !IsOwner || Application.isBatchMode || Current < 0) return;
            sight?.DrawPeriphery();
            sight?.DrawMarkers(Current, selected);
            var panel = new Rect((Screen.width - 430) / 2, Screen.height - 87, 430, 78);
            GUILayout.BeginArea(panel, GUI.skin.box);
            GUILayout.Label("GHOST SIGHT   /   " + session.Containers[Current].transform.parent.name);
            double wait = readyAt.Value - NetworkManager.ServerTime.Time;
            string target = selected >= 0 ? session.Containers[selected].transform.parent.name :
                hovered >= 0 ? session.Containers[hovered].transform.parent.name : "";
            GUILayout.Label(wait > 0 ? $"Recovering  {wait:F1}s" : feedback.Length > 0 ? feedback :
                target.Length > 0 ? target + (selected >= 0 ? "   [E] Transfer" : "   [Click] Select") : "Look for a cyan container");
            GUILayout.Label("Hold RMB: orbit    /    Click: select    /    E: transfer");
            GUILayout.EndArea();
        }

        public override void OnNetworkDespawn()
        {
            sight?.Dispose();
            sight = null;
            container.OnValueChanged -= MoveView;
            if (!IsOwner) return;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }
}
