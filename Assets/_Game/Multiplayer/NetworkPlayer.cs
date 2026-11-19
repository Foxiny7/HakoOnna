using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace HakoOnna.Multiplayer
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class NetworkPlayer : NetworkBehaviour
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
        private float serverYaw;

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
            if (IsOwner && !Application.isBatchMode)
            {
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
            if (Cursor.lockState != CursorLockMode.Locked || !Application.isFocused)
                return;

            if (keyboard != null)
            {
                move = new Vector2(
                    (keyboard.dKey.isPressed ? 1 : 0) - (keyboard.aKey.isPressed ? 1 : 0),
                    (keyboard.wKey.isPressed ? 1 : 0) - (keyboard.sKey.isPressed ? 1 : 0));
                sprint = keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed;
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
                SubmitInputRpc(move, yaw, sprint);

            if (!IsServer)
                return;

            var input = Time.unscaledTime - lastInputAt <= 0.25f ? serverMove : Vector2.zero;
            transform.rotation = Quaternion.Euler(0, serverYaw, 0);
            var direction = transform.right * input.x + transform.forward * input.y;
            var speed = serverSprint ? config.RunSpeed : config.WalkSpeed;
            verticalSpeed = controller.isGrounded ? -2f : verticalSpeed + Physics.gravity.y * Time.fixedDeltaTime;
            controller.Move((direction * speed + Vector3.up * verticalSpeed) * Time.fixedDeltaTime);
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        private void SubmitInputRpc(Vector2 direction, float facingYaw, bool running)
        {
            if (!float.IsFinite(direction.x) || !float.IsFinite(direction.y) || !float.IsFinite(facingYaw))
                return;

            serverMove = Vector2.ClampMagnitude(direction, 1);
            serverYaw = Mathf.Repeat(facingYaw, 360f);
            serverSprint = running;
            lastInputAt = Time.unscaledTime;
        }

        private void LateUpdate()
        {
            if (IsSpawned && IsOwner)
                playerCamera.transform.rotation = Quaternion.Euler(pitch, yaw, 0);
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // Used by the multi-process smoke check through the same owner RPC as keyboard input.
        internal void SupplyTestInput(Vector2 direction, float facingYaw, bool running)
        {
            move = direction;
            yaw = facingYaw;
            sprint = running;
        }
#endif

        public override void OnNetworkDespawn()
        {
            playerCamera.gameObject.SetActive(false);
            if (IsOwner)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }
    }
}
