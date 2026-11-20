using HakoOnna.Rules;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace HakoOnna.Multiplayer
{
    public sealed partial class NetworkPlayer
    {
        private readonly NetworkVariable<int> openBox = new(-1);
        private readonly NetworkVariable<int> boxItem = new(-1, NetworkVariableReadPermission.Owner);
        private readonly NetworkVariable<int> heldItem = new(-1, NetworkVariableReadPermission.Owner);
        private LocalSession session;
        private MatchRules.Player explorer;
        private int searching = -1;
        private Transform standPoint;
        private bool approaching, serverDeep;
        private float approachDeadline;
        private int requested = -1;
        private int aimed = -1;
        private int aimedDoor = -1;
        private GroundItem aimedGround;
        private bool deep;
        private double serverStart;
        private float duration;
        private string feedback = "";
        private bool Observing => requested >= 0 && openBox.Value == requested;
        private int PlayerId => checked((int)OwnerClientId);

        private void InitializeSearch()
        {
            session = FindFirstObjectByType<LocalSession>();
            if (IsServer)
            {
                explorer = session.Rules.AddExplorer(PlayerId);
                heldItem.Value = (int)explorer.Inventory[0].Value;
                foreach (var item in explorer.Inventory)
                    slots.Add((int?)item ?? -1);
            }
            openBox.OnValueChanged += ShowOpenBox;
            if (openBox.Value >= 0)
                session.Containers[openBox.Value].SetOpenImmediate(true);
        }

        private void ShowOpenBox(int previous, int next)
        {
            if (previous >= 0)
                session.Containers[previous].IsOpen = false;
            if (next >= 0)
                session.Containers[next].IsOpen = true;
            if (IsOwner && next >= 0 && next == requested)
            {
                Cursor.lockState = CursorLockMode.Confined;
                Cursor.visible = true;
            }
            if (IsOwner && previous >= 0 && requested == previous && next < 0)
                LeaveInspection();
        }

        private int LookTarget(Vector3 direction)
            => LookTarget(direction, out _);

        private int LookTarget(Vector3 direction, out int door)
            => LookTarget(direction, out door, out _);

        private int LookTarget(Vector3 direction, out int door, out GroundItem ground)
        {
            var eye = transform.position + Vector3.up * config.EyeHeight;
            var ray = new Ray(eye, direction);
            Physics.Raycast(ray, out var hit, config.SearchReach,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            ground = null;
            if (Physics.Raycast(ray, out var itemHit, hit.collider ? hit.distance : config.SearchReach,
                Physics.IgnoreRaycastLayer, QueryTriggerInteraction.Collide) &&
                itemHit.collider.TryGetComponent(out ground))
                hit = itemHit;
            door = FindDoorAtFrame(session.Doors, ray, config.SearchReach, hit);
            if (door >= 0) ground = null;
            if (door >= 0 || !hit.collider) return -1;
            var visual = hit.collider.GetComponentInParent<HakoOnna.Art.ContainerVisual>();
            return visual ? System.Array.IndexOf(session.Containers, visual) : -1;
        }

        private void UpdateSearchInput(Keyboard keyboard)
        {
            var focused = keyboard != null && Application.isFocused &&
                (Observing || Cursor.lockState == CursorLockMode.Locked);
            if (focused)
                ReadEquipmentKeys(keyboard);
            var direction = Quaternion.Euler(pitch, yaw, 0) * Vector3.forward;
            aimedDoor = -1;
            aimedGround = null;
            aimed = focused && requested < 0 ? LookTarget(direction, out aimedDoor, out aimedGround) : -1;
            if (requested >= 0)
            {
                if (!focused || keyboard.escapeKey.wasPressedThisFrame ||
                    (Observing ? keyboard.eKey.wasPressedThisFrame : !(deep ? keyboard.rKey : keyboard.eKey).isPressed))
                {
                    CloseInspection();
                }
                else if (Observing && keyboard.fKey.wasPressedThisFrame)
                    ExchangeRpc(requested, true);
                else if (Observing && keyboard.gKey.wasPressedThisFrame)
                    ExchangeRpc(requested, false);
            }
            else if (aimedGround && keyboard.eKey.wasPressedThisFrame)
                TakeGroundRpc(aimedGround.NetworkObjectId, direction);
            else if (focused && keyboard.gKey.wasPressedThisFrame)
                DropGroundRpc();
            else if (aimedDoor >= 0 && keyboard.eKey.wasPressedThisFrame)
                UseDoorRpc(aimedDoor, direction);
            else if (aimed >= 0 && (keyboard.eKey.wasPressedThisFrame || keyboard.rKey.wasPressedThisFrame))
            {
                requested = aimed;
                PrepareInspection(session.Containers[requested].transform);
                deep = keyboard.rKey.wasPressedThisFrame;
                feedback = "";
                BeginSearchRpc(requested, direction, deep);
            }
        }

        private void CloseInspection()
        {
            LeaveInspection();
            EndSearchRpc();
        }

        private void LeaveInspection()
        {
            requested = -1;
            duration = 0;
            feedback = "";
            Cursor.lockState = Application.isFocused ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !Application.isFocused;
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        private void BeginSearchRpc(int id, Vector3 direction, bool deepSearch)
        {
            session.Rules.AdvanceTo(Time.realtimeSinceStartupAsDouble);
            if (id < 0 || direction.sqrMagnitude < 0.99f || direction.sqrMagnitude > 1.01f ||
                LookTarget(direction) != id || !session.Rules.ReserveSearch(PlayerId, id,
                    deepSearch ? SearchKind.Deep : SearchKind.Normal))
            {
                SearchReplyRpc(id, 0, 0);
                return;
            }
            searching = id;
            standPoint = session.Containers[id].transform.parent.Find("InteractPoint");
            approaching = true;
            serverDeep = deepSearch;
            approachDeadline = Time.unscaledTime + 4;
        }

        [Rpc(SendTo.Owner)]
        private void SearchReplyRpc(int id, double start, float seconds)
        {
            if (requested != id)
                return;
            duration = seconds;
            serverStart = start;
            if (seconds == 0)
            {
                LeaveInspection();
                feedback = "Unavailable. Release and try again.";
            }
        }

        private void TickSearch()
        {
            if (searching < 0)
                return;
            if (explorer.Role == Role.HakoBito && explorer.ContainerId == searching)
            {
                ShowOccupiedOpening();
                return;
            }
            if (approaching)
            {
                var offset = standPoint.position - transform.position;
                var heightError = Mathf.Abs(offset.y);
                offset.y = 0;
                if (Time.unscaledTime > approachDeadline)
                {
                    SearchReplyRpc(searching, 0, 0);
                    EndServerSearch();
                    return;
                }
                if (offset.magnitude > .035f || heightError > .22f)
                {
                    // From the side, clear the front corner before moving laterally.
                    var front = standPoint.forward;
                    var depth = Vector3.Dot(offset, front);
                    var step = depth > .05f ? front * depth : offset;
                    controller.Move(Vector3.ClampMagnitude(step, config.WalkSpeed * Time.fixedDeltaTime));
                    return;
                }
                approaching = false;
                session.Rules.AdvanceTo(Time.realtimeSinceStartupAsDouble);
                session.Rules.StartSearchClock(PlayerId, searching);
                SearchReplyRpc(searching, NetworkManager.ServerTime.Time,
                    (float)config.ToRulesSettings().SearchSeconds * (serverDeep ? 2 : 1));
            }
            // The collider stays in place while the visual opens.
            var visual = session.Containers[searching];
            var collider = visual.GetComponentInChildren<Collider>();
            var eye = transform.position + Vector3.up * config.EyeHeight;
            var toBox = collider.ClosestPoint(eye) - eye;
            if (toBox.magnitude > config.SearchReach || (toBox.sqrMagnitude > 0.0001f &&
                LookTarget(toBox.normalized) != searching))
            {
                SearchReplyRpc(searching, 0, 0);
                EndServerSearch();
                return;
            }
            session.Rules.AdvanceTo(Time.realtimeSinceStartupAsDouble);
            if (explorer.Role == Role.HakoBito && explorer.ContainerId == searching)
            {
                ShowOccupiedOpening();
                return;
            }
            if (openBox.Value < 0 && session.Rules.SearchProgress(searching) >= 1)
            {
                openBox.Value = searching;
                UpdateItems();
            }
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        private void ExchangeRpc(int id, bool take)
        {
            if (id < 0 || id != searching || openBox.Value != id) return;
            var slot = take ? explorer.PickupSlot(selected.Value) : selected.Value;
            if (!session.Rules.ExchangeWithContainer(PlayerId, id, slot, take)) return;
            slots[slot] = (int?)explorer.Inventory[slot] ?? -1;
            selected.Value = slot;
            lampOn.Value = false;
            UpdateItems();
        }

        private void ShowOccupiedOpening()
        {
            int id = searching;
            EndServerSearch();
            for (int i = 0; i < slots.Count; i++) slots[i] = -1;
            heldItem.Value = -1;
            lampOn.Value = false;
            foreach (var peer in NetworkManager.ConnectedClientsList)
            {
                if (!peer.PlayerObject) continue;
                bool watches = peer.ClientId == OwnerClientId ||
                    (peer.PlayerObject.TryGetComponent<NetworkHider>(out var hider) ? hider.Current == id :
                    Vector3.Distance(peer.PlayerObject.transform.position, session.Containers[id].transform.position) < 12);
                if (watches) GrabRpc(id, RpcTarget.Single(peer.ClientId, RpcTargetUse.Temp));
            }
        }

        [Rpc(SendTo.SpecifiedInParams)]
        private void GrabRpc(int id, RpcParams recipients = default)
        {
            if (IsOwner && requested == id) LeaveInspection();
            session.StartCoroutine(session.PlayOccupied(id, transform));
        }

        private void UpdateItems()
        {
            boxItem.Value = (int?)session.Rules.ContainerItem(searching) ?? -1;
            heldItem.Value = slots[selected.Value];
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        private void EndSearchRpc() => EndServerSearch();

        private void EndServerSearch()
        {
            if (searching < 0)
                return;
            session.Rules.EndSearch(PlayerId, searching);
            searching = -1;
            approaching = false;
            openBox.Value = -1;
            boxItem.Value = -1;
        }

        private void ReleaseSearch()
        {
            openBox.OnValueChanged -= ShowOpenBox;
            if (session && openBox.Value >= 0)
                ShowOpenBox(openBox.Value, -1);
            if (IsServer && session)
                session.Rules.RemoveExplorer(PlayerId);
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        internal void CheckContainerExchange(HakoOnna.Art.ContainerVisual visual, int id, ref int step, ref bool exchanged)
        {
            if (step == 0)
            {
                var eye = transform.position + Vector3.up * config.EyeHeight;
                var direction = (visual.GetComponentInChildren<Collider>().bounds.center - eye).normalized;
                if (LookTarget(direction) != id) return;
                BeginSearchRpc(id, direction, false);
                step++;
            }
            else if (step == 1 && openBox.Value == id)
            {
                ExchangeRpc(id, false);
                step++;
            }
            else if (step == 2 && boxItem.Value == (int)ItemKind.Flashlight && heldItem.Value < 0)
            {
                SelectSlotRpc(8, false);
                ExchangeRpc(id, true);
                step++;
            }
            else if (step == 3 && selected.Value == 8 && boxItem.Value < 0 && heldItem.Value == (int)ItemKind.Flashlight)
            {
                ToggleLampRpc();
                step++;
            }
            else if (step == 4 && beam.isActiveAndEnabled)
            {
                SelectSlotRpc(1, true);
                step++;
            }
            else if (step == 5 && selected.Value == 0 && !lampOn.Value && !beam.isActiveAndEnabled)
            {
                exchanged = true;
                EndSearchRpc();
                step++;
            }
        }
#endif

        private void OnGUI()
        {
            if (!IsSpawned || !IsOwner || Application.isBatchMode)
                return;
            DrawHotbar();
            var meter = new Rect(18, Screen.height - 48, 136, 12);
            GUI.color = new Color(.15f,.15f,.15f);
            GUI.DrawTexture(meter, Texture2D.whiteTexture);
            meter.width *= stamina.Value;
            GUI.color = exhausted.Value ? new Color(.94f,.43f,.20f) : new Color(.46f,.80f,.65f);
            GUI.DrawTexture(meter, Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(18, Screen.height - 74, 160, 24), exhausted.Value ? "Exhausted" : "Stamina");
            if (Observing)
            {
                if (boxItem.Value >= 0 && itemAnchor)
                {
                    var point = playerCamera.WorldToScreenPoint(itemAnchor.position);
                    if (point.z > 0 && GUI.Button(new Rect(Mathf.Clamp(point.x - 55, 0, Screen.width - 110),
                        Mathf.Clamp(Screen.height - point.y - 12, 0, Screen.height - 85), 110, 26),
                        ((ItemKind)boxItem.Value).ToString()))
                        ExchangeRpc(requested, true);
                }
                GUI.Box(new Rect(Screen.width / 2 - 135, Screen.height - 106, 270, 24),
                    boxItem.Value < 0 ? "Empty container  |  G: Store" : "F: Take  /  Click item");
                if (GUI.Button(new Rect(Screen.width / 2 - 55, Screen.height - 79, 110, 24), "Close [E]"))
                    CloseInspection();
                return;
            }
            GUI.Label(new Rect(Screen.width / 2 - 4, Screen.height / 2 - 10, 20, 24), "+");
            if (aimedGround && requested < 0)
            {
                bool full = true;
                foreach (var item in slots)
                    if (item < 0) { full = false; break; }
                GUI.Box(new Rect(Screen.width / 2 - 110, Screen.height / 2 + 18, 220, 24),
                    full ? "Inventory full" : "E  |  Take " + aimedGround.Kind.Value);
                return;
            }
            if (aimedDoor >= 0 && requested < 0)
            {
                var door = session.Doors[aimedDoor];
                GUI.Box(new Rect(Screen.width / 2 - 65, Screen.height / 2 + 18, 130, 24),
                    door.Locked ? "E  |  Try door" : door.IsOpen ? "E  |  Close door" : "E  |  Open door");
                return;
            }
            if (aimed < 0 && requested < 0)
                return;
            string prompt = "Hold E to search | Hold R for deep search";
            if (requested >= 0 && duration == 0) prompt = "Moving into position...";
            if (feedback.Length > 0)
                prompt = feedback;
            else if (requested >= 0 && duration > 0)
            {
                var progress = Mathf.Clamp01((float)(NetworkManager.ServerTime.Time - serverStart) / duration);
                prompt = $"Searching {progress:P0}";
                GUI.HorizontalSlider(new Rect(Screen.width / 2 - 90, Screen.height / 2 + 45, 180, 16), progress, 0, 1);
            }
            GUI.Box(new Rect(Screen.width / 2 - 155, Screen.height / 2 + 18, 310, 24), prompt);
        }
    }
}
