using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace HakoOnna.Multiplayer
{
    [RequireComponent(typeof(CharacterController))]
    public sealed partial class NetworkPlayer : NetworkBehaviour
    {
        [SerializeField] private GameConfig config;
        [SerializeField] private Camera playerCamera;
        [SerializeField] private Renderer body;
        [SerializeField] private GameObject arms;

        private CharacterController controller;
        private Vector2 move;
        private bool sprint;
        private float yaw;
        private float pitch;
        private float verticalSpeed;
        private float lastInputAt;
        private Vector2 serverMove;
        private bool serverSprint;
        private bool jumpQueued, serverJump;
        private readonly NetworkVariable<float> stamina = new(1, NetworkVariableReadPermission.Owner);
        private readonly NetworkVariable<bool> exhausted = new(false, NetworkVariableReadPermission.Owner);
        private float recoveryAt;
        private float serverYaw;
        internal GameConfig Config => config;

        public override void OnNetworkSpawn()
        {
            controller = GetComponent<CharacterController>();
            ConfigureDimensions();
            controller.enabled = IsServer;
            playerCamera.gameObject.SetActive(IsOwner && !Application.isBatchMode);
            arms.SetActive(IsOwner);
            body.shadowCastingMode = IsOwner ? ShadowCastingMode.ShadowsOnly : ShadowCastingMode.On;
            yaw = transform.eulerAngles.y;
            serverYaw = yaw;
            InitializeSearch();
            InitializeEquipment();
            if (session.TestBox >= 0 && IsOwner)
            {
                var target = session.Containers[session.TestBox].GetComponentInChildren<Collider>().bounds.center;
                var look = Quaternion.LookRotation(target - playerCamera.transform.position).eulerAngles;
                pitch = Mathf.DeltaAngle(0, look.x);
                yaw = look.y;
                ToggleLampRpc();
            }
            if (IsOwner && !IsServer) RequestDoorsRpc();
            if (IsOwner && !Application.isBatchMode)
            {
                playerCamera.gameObject.AddComponent<HouseAmbience>().Begin(session.Foley);
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }

        public void ConfigureDimensions()
        {
            var character = GetComponent<CharacterController>();
            character.enabled = false;
            character.height = config.PlayerHeight;
            character.radius = config.PlayerRadius;
            character.center = Vector3.up * (config.PlayerHeight * 0.5f);
            character.stepOffset = 0.15f;
            character.skinWidth = 0.03f;
            character.minMoveDistance = 0;
            body.transform.localPosition = character.center;
            body.transform.localScale = new Vector3(config.PlayerRadius * 2, config.PlayerHeight * 0.5f,
                config.PlayerRadius * 2);
            playerCamera.transform.localPosition = Vector3.up * config.EyeHeight;
            arms.transform.localScale = Vector3.one * 0.75f;
        }

        private void Update()
        {
            if (!IsSpawned || !IsOwner)
                return;

            move = Vector2.zero;
            sprint = false;
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            UpdateSearchInput(keyboard);
            if (requested >= 0 || Cursor.lockState != CursorLockMode.Locked || !Application.isFocused)
                return;

            if (keyboard != null)
            {
                move = new Vector2(
                    (keyboard.dKey.isPressed ? 1 : 0) - (keyboard.aKey.isPressed ? 1 : 0),
                    (keyboard.wKey.isPressed ? 1 : 0) - (keyboard.sKey.isPressed ? 1 : 0));
                sprint = keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed;
                jumpQueued |= keyboard.spaceKey.wasPressedThisFrame;
            }
            if (Mouse.current != null)
            {
                var delta = Mouse.current.delta.ReadValue() * config.MouseSensitivity;
                yaw = Mathf.Repeat(yaw + delta.x, 360f);
                pitch = Mathf.Clamp(pitch - delta.y, -85f, 85f);
            }
        }

        private void FixedUpdate()
        {
            if (!IsSpawned)
                return;

            if (IsOwner)
            {
                SubmitInputRpc(move, yaw, sprint, new Vector2(playerCamera.transform.eulerAngles.x, playerCamera.transform.eulerAngles.y), jumpQueued);
                jumpQueued = false;
            }

            if (!IsServer)
                return;

            var input = searching < 0 && Time.unscaledTime - lastInputAt <= 0.25f ? serverMove : Vector2.zero;
            var facing = Quaternion.Euler(0, serverYaw, 0);
            if (searching >= 0)
                facing = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(-standPoint.forward),
                    360 * Time.fixedDeltaTime);
            transform.rotation = facing;
            var direction = transform.right * input.x + transform.forward * input.y;
            bool running = serverSprint && input.sqrMagnitude > .01f && !exhausted.Value;
            var speed = running ? config.RunSpeed : config.WalkSpeed;
            verticalSpeed = controller.isGrounded ? -2f : verticalSpeed + Physics.gravity.y * Time.fixedDeltaTime;
            if (serverJump && searching < 0 && controller.isGrounded)
                verticalSpeed = Mathf.Sqrt(-2 * Physics.gravity.y * config.JumpHeight);
            serverJump = false;
            var previous = transform.position;
            var flags = controller.Move((direction * speed + Vector3.up * verticalSpeed) * Time.fixedDeltaTime);
            if ((flags & CollisionFlags.Above) != 0 && verticalSpeed > 0) verticalSpeed = 0;
            if (running && Vector3.ProjectOnPlane(transform.position - previous, Vector3.up).sqrMagnitude > .000001f)
            {
                stamina.Value = Mathf.Max(0, stamina.Value - Time.fixedDeltaTime / config.SprintSeconds);
                exhausted.Value = stamina.Value <= 0;
                recoveryAt = Time.fixedTime + config.RecoveryDelay;
            }
            else if (Time.fixedTime >= recoveryAt && stamina.Value < 1)
            {
                stamina.Value = Mathf.Min(1, stamina.Value + Time.fixedDeltaTime / config.RecoverySeconds);
                if (stamina.Value >= config.ResumeStamina) exhausted.Value = false;
            }
            TickSearch();
            TickFootsteps(transform.position - previous, (flags & CollisionFlags.Below) != 0, running);
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        private void SubmitInputRpc(Vector2 direction, float facingYaw, bool running, Vector2 viewAngles, bool jump)
        {
            if (!float.IsFinite(direction.x) || !float.IsFinite(direction.y) || !float.IsFinite(facingYaw))
                return;

            serverMove = Vector2.ClampMagnitude(direction, 1);
            if (lampOn.Value && float.IsFinite(viewAngles.x) && float.IsFinite(viewAngles.y))
                lampAngles.Value = viewAngles;
            serverYaw = Mathf.Repeat(facingYaw, 360f);
            serverSprint = running;
            serverJump |= jump && searching < 0;
            lastInputAt = Time.unscaledTime;
        }

        private void LateUpdate()
        {
            if (!IsSpawned) return;
            if (IsOwner)
            {
                UpdateInspectionPose();
                if (footsteps && footsteps.isPlaying && !OwnerWalking) footsteps.Stop();
            }
            UpdateEquipmentPose();
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // Used by the multi-process smoke check through the same owner RPC as keyboard input.
        internal bool IsExhausted => exhausted.Value;
        internal void SupplyTestInput(Vector2 direction, float facingYaw, bool running, bool jump = false)
        {
            move = direction;
            yaw = facingYaw;
            sprint = running;
            jumpQueued |= jump;
        }
#endif

        public override void OnNetworkDespawn()
        {
            ReleaseSearch();
            playerCamera.gameObject.SetActive(false);
            if (IsOwner)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }
    }
}
