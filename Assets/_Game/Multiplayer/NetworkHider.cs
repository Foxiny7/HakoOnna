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
        private Vector2 scroll;
        private bool browsing = true;
        private float yaw, pitch;
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
            SetBrowse(true);
        }

        private void MoveView(int previous, int next)
        {
            if (next < 0) return;
            var marker = session.Containers[next].transform.parent.Find("InteractPoint");
            var bounds = session.Containers[next].GetComponentInChildren<Collider>().bounds;
            var outward = marker.forward;
            // Local presentation only. Keep the network root at its neutral spawn.
            var front = bounds.ClosestPoint(marker.position + Vector3.up * bounds.extents.y);
            view.position = front + outward * .10f;
            body.position = view.position - outward * (session.Config.PlayerRadius + .12f)
                - Vector3.up * (session.Config.EyeHeight - session.Config.PlayerHeight / 2);
            yaw = marker.eulerAngles.y;
            pitch = 0;
            view.rotation = Quaternion.Euler(pitch, yaw, 0);
            feedback = "";
        }

        internal void Transfer(int destination) => TransferRpc(destination);

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        private void TransferRpc(int destination)
        {
            session.Rules.AdvanceTo(Time.realtimeSinceStartupAsDouble);
            if (!session.Rules.TryTransfer(checked((int)OwnerClientId), destination))
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

        private void SetBrowse(bool open)
        {
            browsing = open;
            Cursor.lockState = open ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = open;
        }

        private void Update()
        {
            if (!IsOwner || !IsSpawned || Application.isBatchMode || !Application.isFocused) return;
            if (Keyboard.current?.tabKey.wasPressedThisFrame == true) SetBrowse(!browsing);
            if (Keyboard.current?.escapeKey.wasPressedThisFrame == true) SetBrowse(true);
            if (browsing || Mouse.current == null) return;
            var delta = Mouse.current.delta.ReadValue() * session.Config.MouseSensitivity;
            yaw += delta.x;
            pitch = Mathf.Clamp(pitch - delta.y, -80, 80);
            view.rotation = Quaternion.Euler(pitch, yaw, 0);
        }

        private void OnGUI()
        {
            if (!IsSpawned || !IsOwner || Application.isBatchMode || Current < 0) return;
            GUILayout.BeginArea(new Rect(Screen.width - 285, 16, 270, Mathf.Max(120, Screen.height - 32)), GUI.skin.box);
            GUILayout.Label("Hiding in " + session.Containers[Current].transform.parent.name);
            if (GUILayout.Button(browsing ? "Look around [Tab]" : "Choose container [Tab]")) SetBrowse(!browsing);
            if (browsing)
            {
                double remaining = readyAt.Value - NetworkManager.ServerTime.Time;
                GUILayout.Label(remaining > 0 ? $"Transfer in {remaining:F1}s" : "Choose a destination");
                if (feedback.Length > 0) GUILayout.Label(feedback);
                scroll = GUILayout.BeginScrollView(scroll);
                for (int i = 0; i < session.Containers.Length; i++)
                {
                    GUI.enabled = remaining <= 0 && i != Current;
                    if (GUILayout.Button(session.Containers[i].transform.parent.name)) Transfer(i);
                }
                GUI.enabled = true;
                GUILayout.EndScrollView();
            }
            GUILayout.EndArea();
        }

        public override void OnNetworkDespawn()
        {
            container.OnValueChanged -= MoveView;
            if (!IsOwner) return;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }
}
