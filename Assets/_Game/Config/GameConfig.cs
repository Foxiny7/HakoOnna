using HakoOnna.Rules;
using UnityEngine;

namespace HakoOnna
{
    [CreateAssetMenu(menuName = "Hako Onna/Game Config", fileName = "GameConfig")]
    public sealed class GameConfig : ScriptableObject
    {
        [SerializeField, Min(0.01f)] private float searchSeconds = 2f;
        [SerializeField, Min(0f)] private float transferCooldownSeconds = 3f;
        [SerializeField, Min(0.01f)] private float walkSpeed = 3f;
        [SerializeField, Min(0.01f)] private float runSpeed = 5f;
        [SerializeField, Min(0.001f)] private float mouseSensitivity = 0.1f;
        [SerializeField, Min(0.1f)] private float playerHeight = 1.65f;
        [SerializeField, Min(0.01f)] private float playerRadius = 0.22f;
        [SerializeField, Min(0.1f)] private float eyeHeight = 1.45f;

        [SerializeField, Min(0.1f)] private float searchReach = 2.5f;
        public float SearchReach => searchReach;
        public float WalkSpeed => walkSpeed;
        public float RunSpeed => runSpeed;
        [SerializeField, Min(.1f)] private float jumpHeight = .65f;
        [SerializeField, Min(.1f)] private float sprintSeconds = 6;
        [SerializeField, Min(.1f)] private float recoverySeconds = 8;
        [SerializeField, Min(0)] private float recoveryDelay = 1;
        [SerializeField, Range(.01f, 1)] private float resumeStamina = .3f;
        public float JumpHeight => jumpHeight;
        public float SprintSeconds => sprintSeconds;
        public float RecoverySeconds => recoverySeconds;
        public float RecoveryDelay => recoveryDelay;
        public float ResumeStamina => resumeStamina;
        public float MouseSensitivity => mouseSensitivity;
        public float PlayerHeight => playerHeight;
        public float PlayerRadius => playerRadius;
        public float EyeHeight => eyeHeight;

        public RulesSettings ToRulesSettings()
        {
            return new RulesSettings(searchSeconds, transferCooldownSeconds);
        }
    }
}
