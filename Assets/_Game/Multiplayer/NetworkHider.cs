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
            view.gameObject.AddComponent<HouseAmbience>().Begin(session.Foley);
            body.gameObject.SetActive(true);
            sight = new GhostSight(view.GetComponent<Camera>(), body, session);
            MoveView(-1, Current);
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
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
            yaw = marker.eulerAngles.y;
            pitch = 0;
            float head = body.localScale.y;
            float clearance = Mathf.Min(.06f, (bounds.extents.y - head) * .5f);
            // Show the torso below eye level; the cylinder's flat head would cover the lower view.
            var torso = body.localScale;
            torso.y *= .55f;
            body.localScale = torso;
            body.position -= Vector3.up * (head - torso.y);
            view.SetPositionAndRotation(bounds.center + Vector3.up * (head + clearance),
                Quaternion.Euler(pitch, yaw, 0));
            sight?.Show(next);
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

        private void Update()
        {
            if (!IsOwner || !IsSpawned || Application.isBatchMode || !Application.isFocused) return;
            var mouse = Mouse.current;
            if (sight == null || mouse == null) return;
            if (Keyboard.current?.escapeKey.wasPressedThisFrame == true)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            if (Cursor.lockState != CursorLockMode.Locked)
            {
                hovered = -1;
                return;
            }

            var delta = mouse.delta.ReadValue() * session.Config.MouseSensitivity;
            yaw = Mathf.Repeat(yaw + delta.x, 360f);
            pitch = Mathf.Clamp(pitch - delta.y, -85, 85);
            view.rotation = Quaternion.Euler(pitch, yaw, 0);
            var center = new Vector2(Screen.width * .5f, Screen.height * .5f);
            hovered = sight.Pick(center, Current, selected, mouse.leftButton.wasPressedThisFrame);
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
            if (Cursor.lockState == CursorLockMode.Locked)
                GUI.Label(new Rect(Screen.width / 2 - 4, Screen.height / 2 - 10, 20, 24), "+");
            var panel = new Rect((Screen.width - 430) / 2, Screen.height - 87, 430, 78);
            GUILayout.BeginArea(panel, GUI.skin.box);
            GUILayout.Label("GHOST SIGHT   /   " + session.Containers[Current].transform.parent.name);
            double wait = readyAt.Value - NetworkManager.ServerTime.Time;
            string target = selected >= 0 ? session.Containers[selected].transform.parent.name :
                hovered >= 0 ? session.Containers[hovered].transform.parent.name : "";
            GUILayout.Label(wait > 0 ? $"Recovering  {wait:F1}s" : feedback.Length > 0 ? feedback :
                target.Length > 0 ? target + (selected >= 0 ? "   [E] Transfer" : "   [Click] Select") : "Look for a cyan container");
            if (Cursor.lockState == CursorLockMode.Locked)
                GUILayout.Label("Mouse: look    /    Click: select    /    E: transfer    /    Esc: cursor");
            else if (GUILayout.Button("Resume mouse look"))
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
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
