using System;
using System.Collections.Generic;
using UnityEngine;

namespace HakoOnna.Art
{
    /// <summary>
    /// 推拉门的表现层（美术负责，不含玩法逻辑）。
    ///
    /// 门扇挂在门洞一侧墙面的上轨道上，沿墙滑动：
    ///   开度 0 = 关（门扇盖住门洞），1 = 开（门扇停在门洞旁边的墙面上，门洞全宽可走）。
    /// 每扇门扇是一个子物体（Leaf_*），带 BoxCollider 和运动学 Rigidbody：关着时挡人，开着时贴在墙上。
    ///
    /// 程序（服务器权威）只需要：
    ///   · 设置 IsOpen（播放开 / 关动画）；中途加入、重连时用 SetOpenImmediate 直接到位；
    ///   · 准星打到的碰撞体用 DoorVisual.FromCollider(hit.collider) 判断是不是门；
    ///   · 关门前可以用 WorldClosedVolume 检查门口有没有人（这个组件不检查）。
    /// 锁着的门（縁側朝院子的玻璃拉门）：IsOpen 不起作用，交互时调用 Rattle() 晃一下。
    /// 场景是生成的，不要在场景里给门加组件；联网逻辑请在运行时挂（和容器一样）。
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public class DoorVisual : MonoBehaviour
    {
        [Serializable]
        public class Leaf
        {
            public Transform part;
            [Tooltip("完全打开时相对关闭位置的本地位移（沿墙）")] public Vector3 openOffset;
            [HideInInspector] public Vector3 closedPos;
            [HideInInspector] public bool cached;
        }

        static readonly List<DoorVisual> all = new List<DoorVisual>();

        /// <summary>场景里当前启用的所有门（含锁着的玻璃门）。</summary>
        public static IReadOnlyList<DoorVisual> All => all;

        [SerializeField] Leaf[] leaves = Array.Empty<Leaf>();
        [Tooltip("锁着的门不会打开，只能 Rattle()")] [SerializeField] bool locked;
        [Tooltip("开局状态")] [SerializeField] bool startOpen = true;
        [Tooltip("打开动画时长（秒）")] [SerializeField] float openSeconds = .55f;
        [Tooltip("关闭动画时长（秒）")] [SerializeField] float closeSeconds = .45f;
        [Tooltip("门洞（本地坐标）")] [SerializeField] Bounds opening;
        [Tooltip("门扇全部关上时占的空间（本地坐标）")] [SerializeField] Bounds closedVolume;
        [Tooltip("仅编辑器预览用：拖动查看开关，运行时无效")]
        [Range(0f, 1f)] [SerializeField] float editorPreview = 1f;

        bool isOpen;
        bool started;
        float amount;       // 当前开度 0..1
        float rattleAge = -1f;

        /// <summary>开门动画开始时触发（接拉门音效）。</summary>
        public event Action Opened;
        /// <summary>关门动画开始时触发（接拉门音效）。</summary>
        public event Action Closed;
        /// <summary>锁着的门被晃了一下（接“咔哒咔哒”音效、提示文字）。</summary>
        public event Action Rattled;

        public bool Locked => locked;
        /// <summary>开局状态（锁着的门永远是 false）。</summary>
        public bool StartsOpen => startOpen && !locked;
        /// <summary>当前动画开度：0 = 完全关上，1 = 完全打开。</summary>
        public float Amount => amount;
        /// <summary>动画还没走完。</summary>
        public bool IsMoving => !Mathf.Approximately(amount, isOpen ? 1f : 0f);
        /// <summary>门扇全部关上时占的世界空间（关门前查有没有人站在门口用）。</summary>
        public Bounds WorldClosedVolume => ToWorld(closedVolume);
        /// <summary>门洞的世界空间范围。</summary>
        public Bounds WorldOpening => ToWorld(opening);

        /// <summary>目标状态。设置后播放动画；锁着的门忽略。</summary>
        public bool IsOpen
        {
            get => isOpen;
            set
            {
                EnsureStarted();
                if (locked || isOpen == value) return;
                isOpen = value;
                if (value) Opened?.Invoke();
                else Closed?.Invoke();
            }
        }

        /// <summary>跳过动画直接到位（中途加入、重连时同步状态）。</summary>
        public void SetOpenImmediate(bool open)
        {
            EnsureStarted();
            isOpen = open && !locked;
            amount = isOpen ? 1f : 0f;
            Apply(amount, 0f);
        }

        /// <summary>晃一下（锁着的门被拉的时候；没锁的门也能用，例如猎杀时的效果）。</summary>
        public void Rattle()
        {
            rattleAge = 0f;
            Rattled?.Invoke();
        }

        /// <summary>碰撞体属于哪扇门：门扇自己的碰撞体，或者玻璃门对应的 Window_Glass（DoorLink）。不是门返回 null。</summary>
        public static DoorVisual FromCollider(Collider c)
        {
            if (c == null) return null;
            var link = c.GetComponent<DoorLink>();
            if (link != null && link.Door != null) return link.Door;
            return c.GetComponentInParent<DoorVisual>();
        }

        /// <summary>编辑器生成场景时调用：记录关闭姿态、门洞和关门占的空间，并摆到开局状态。</summary>
        public void Configure(Leaf[] newLeaves, Bounds localOpening, Bounds localClosedVolume, bool isLocked, bool open,
            float openTime, float closeTime)
        {
            leaves = newLeaves ?? Array.Empty<Leaf>();
            foreach (var l in leaves)
            {
                if (l == null || l.part == null) continue;
                l.closedPos = l.part.localPosition;
                l.cached = true;
            }
            opening = localOpening;
            closedVolume = localClosedVolume;
            locked = isLocked;
            startOpen = open && !isLocked;
            openSeconds = openTime;
            closeSeconds = closeTime;
            editorPreview = startOpen ? 1f : 0f;
            Apply(editorPreview, 0f);
        }

        void OnEnable()
        {
            if (!all.Contains(this)) all.Add(this);
            if (Application.isPlaying) EnsureStarted();
            else Apply(editorPreview, 0f);
        }

        void OnDisable() => all.Remove(this);

        void OnValidate()
        {
            if (!Application.isPlaying) Apply(editorPreview, 0f);
        }

        void EnsureStarted()
        {
            if (started || !Application.isPlaying) return;
            started = true;
            isOpen = startOpen && !locked;
            amount = isOpen ? 1f : 0f;
            Apply(amount, 0f);
        }

        void Update()
        {
            if (!Application.isPlaying) return;
            EnsureStarted();
            float target = isOpen ? 1f : 0f;
            bool moved = false;
            if (!Mathf.Approximately(amount, target))
            {
                float seconds = isOpen ? openSeconds : closeSeconds;
                amount = Mathf.MoveTowards(amount, target, Time.deltaTime / Mathf.Max(.01f, seconds));
                moved = true;
            }
            float shake = 0f;
            if (rattleAge >= 0f)
            {
                rattleAge += Time.deltaTime;
                const float life = .45f;
                if (rattleAge >= life) rattleAge = -1f;
                else shake = Mathf.Sin(rattleAge * 70f) * Mathf.Exp(-rattleAge * 7f) * .009f;
                moved = true;
            }
            if (moved) Apply(amount, shake);
        }

        void Apply(float value, float shake)
        {
            if (leaves == null) return;
            float t = Mathf.SmoothStep(0f, 1f, value);
            for (int i = 0; i < leaves.Length; i++)
            {
                var l = leaves[i];
                if (l == null || l.part == null || !l.cached) continue;
                var along = l.openOffset.sqrMagnitude > 1e-8f ? l.openOffset.normalized : Vector3.right;
                // 晃的时候相邻门扇反向，像在轨道里碰撞
                float s = (i & 1) == 0 ? shake : -shake * .8f;
                l.part.localPosition = l.closedPos + l.openOffset * t + along * s;
            }
        }

        Bounds ToWorld(Bounds local)
        {
            var b = new Bounds(transform.TransformPoint(local.center), Vector3.zero);
            var e = local.extents;
            for (int i = 0; i < 8; i++)
                b.Encapsulate(transform.TransformPoint(local.center + new Vector3(
                    (i & 1) == 0 ? -e.x : e.x, (i & 2) == 0 ? -e.y : e.y, (i & 4) == 0 ? -e.z : e.z)));
            return b;
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, .6f, .1f, .6f);
            var w = WorldOpening;
            Gizmos.DrawWireCube(w.center, w.size);
            Gizmos.color = new Color(.2f, .8f, 1f, .6f);
            var c = WorldClosedVolume;
            Gizmos.DrawWireCube(c.center, c.size);
        }
    }
}
