using System;
using UnityEngine;

namespace HakoOnna.Art
{
    /// <summary>
    /// 容器的表现层（美术负责）。
    ///
    /// 时序约定：
    ///   读条（开箱进度）期间 —— 容器保持关闭，进度只在 UI 上显示，本组件不参与。
    ///   读条完成            —— 程序设置 IsOpen = true，播放打开动画。
    ///   玩家停止交互        —— 程序设置 IsOpen = false，播放关闭动画。
    ///   开箱致死            —— 程序调用 PlayGrab()（不设置 IsOpen）。
    ///
    /// 没有 Animator 时用代码驱动门、抽屉、盖子；以后加了 Animator，
    /// 会同时收到 bool 参数 "IsOpen" 和 trigger "Grab"。
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public class ContainerVisual : MonoBehaviour
    {
        [Serializable]
        public class MovingPart
        {
            public Transform part;
            [Tooltip("完全打开时相对关闭位置的本地位移")] public Vector3 openOffset;
            [Tooltip("完全打开时相对关闭姿态的本地旋转（度）")] public Vector3 openEuler;
            [HideInInspector] public Vector3 closedPos;
            [HideInInspector] public Quaternion closedRot = Quaternion.identity;
            [HideInInspector] public bool cached;
        }

        static readonly int IsOpenId = Animator.StringToHash("IsOpen");
        static readonly int GrabId = Animator.StringToHash("Grab");

        [SerializeField] MovingPart[] parts = Array.Empty<MovingPart>();
        [SerializeField] Animator animator;
        [Tooltip("打开动画时长（秒）")] [SerializeField] float openSeconds = 0.35f;
        [Tooltip("关闭动画时长（秒）")] [SerializeField] float closeSeconds = 0.3f;
        [Tooltip("仅编辑器预览用：拖动查看打开姿态，运行时无效")]
        [Range(0f, 1f)] [SerializeField] float editorPreview;

        bool isOpen;
        float amount; // 当前动画位置 0 = 关，1 = 开

        /// <summary>打开动画开始时触发（可接开门音效）。</summary>
        public event Action Opened;
        /// <summary>关闭动画开始时触发（可接关门音效）。</summary>
        public event Action Closed;
        /// <summary>拉入动画开始时触发（可接音效或镜头效果）。</summary>
        public event Action GrabPlayed;

        /// <summary>读条完成后设为 true；停止交互后设为 false。</summary>
        public bool IsOpen
        {
            get => isOpen;
            set
            {
                if (isOpen == value) return;
                isOpen = value;
                if (HasController()) animator.SetBool(IsOpenId, value);
                if (value) Opened?.Invoke();
                else Closed?.Invoke();
            }
        }

        /// <summary>跳过动画直接到位（例如中途加入或重连时同步状态）。</summary>
        public void SetOpenImmediate(bool open)
        {
            isOpen = open;
            amount = open ? 1f : 0f;
            if (HasController()) animator.SetBool(IsOpenId, open);
            Apply(amount);
        }

        public void PlayGrab()
        {
            if (HasController()) animator.SetTrigger(GrabId);
            GrabPlayed?.Invoke();
        }

        /// <summary>编辑器生成预制体时调用，记录关闭姿态。</summary>
        public void ConfigureParts(MovingPart[] newParts)
        {
            parts = newParts ?? Array.Empty<MovingPart>();
            CacheClosedPose(true);
            Apply(0f);
        }

        void OnEnable()
        {
            CacheClosedPose(false);
            Apply(Application.isPlaying ? amount : editorPreview);
        }

        void OnValidate()
        {
            CacheClosedPose(false);
            if (!Application.isPlaying) Apply(editorPreview);
        }

        void Update()
        {
            if (!Application.isPlaying) return;
            float target = isOpen ? 1f : 0f;
            if (Mathf.Approximately(amount, target)) return;
            float seconds = isOpen ? openSeconds : closeSeconds;
            amount = Mathf.MoveTowards(amount, target, Time.deltaTime / Mathf.Max(0.01f, seconds));
            Apply(amount);
        }

        void CacheClosedPose(bool force)
        {
            if (parts == null) return;
            foreach (var p in parts)
            {
                if (p == null || p.part == null) continue;
                if (p.cached && !force) continue;
                p.closedPos = p.part.localPosition;
                p.closedRot = p.part.localRotation;
                p.cached = true;
            }
        }

        void Apply(float value)
        {
            if (parts == null) return;
            float t = Mathf.SmoothStep(0f, 1f, value);
            foreach (var p in parts)
            {
                if (p == null || p.part == null || !p.cached) continue;
                p.part.localPosition = p.closedPos + p.openOffset * t;
                p.part.localRotation = p.closedRot * Quaternion.Euler(p.openEuler * t);
            }
        }

        bool HasController() =>
            Application.isPlaying && animator != null && animator.isActiveAndEnabled &&
            animator.runtimeAnimatorController != null;
    }
}
