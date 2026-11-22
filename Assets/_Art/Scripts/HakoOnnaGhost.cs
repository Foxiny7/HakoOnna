using System;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace HakoOnna.Art
{
    /// <summary>
    /// 箱女（开箱突脸用的模型）的表现层（美术负责），在预制体 Resources/HakoOnna_Ghost 的根节点上。
    ///
    /// 程序这样用（美术设定 4.3；接入步骤见 Assets/_Art/README_美术接入说明.md 的“开箱死亡演出”）：
    ///   var ghost = HakoOnnaGhost.Spawn(box);   // box = 容器 Visual 上的 ContainerVisual；她出现在这个容器的藏匿姿势
    ///   ghost.Lunge = 1; var lungeEyes = ghost.Face.position; ghost.Lunge = 0;   // 容器开着时量：突脸终点的两眼，镜头拽到离它 0.2 m
    ///   ghost.Lunge = t;                        // 0 = 藏匿姿势，1 = 突脸终点（双手抓住探索者的头、嘴张到最大）；时间曲线程序定
    ///   Destroy(ghost.gameObject);              // 演出结束
    /// Face：两眼中间，跟着头动，+Z 从脸朝外。
    ///
    /// 姿势是在“家具放大 1.25 倍”的容器里摆的，所以挂在容器根节点下（箪笥挂在 Drawer_0 下，跟着抽屉动），
    /// 位置 0、不旋转、缩放 1/1.25；场景里的容器放大 1.25 倍后，她正好 1.2 m 高。
    /// 0 到 1 之间是 Blender 里做好的一段动作（Lunge_<容器>，Tools~/ghost_motion.py）：胯先往回一缩，头先甩出去并冲过头，
    /// 躯干跟上，手臂晚出发、最后到，手指在最后收紧，嘴在半路撕开。Lunge 就是这段动作的时间（0..1）。
    ///
    /// 活体细节（只在运行时）：Lunge 停在 1 之后她不是静止的 —— 下巴发颤、头和脖子微抖、胸口喘、手和手指一下下收紧，
    /// 强度随 Lunge 变大；藏着的时候（Lunge = 0）只有很轻的一点。同样是纯表现、各客户端自己算。
    ///
    /// 头发、袖子、下摆：每个姿势里它们怎么垂是 Blender 里按重力解算好、烘在动作里的（Tools~/ghost_soft.py）。
    /// 运行时这里再加一层甩动：骨头 Hair_* / SleeveBag_* / Hem_* 的末端带惯性地追静态位置，她冲出来时往后拖，
    /// 停住后荡回去。纯表现、各客户端自己算，不影响 Face 和其它骨头；编辑器预览和批处理检查里不甩。
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Animator))]
    public class HakoOnnaGhost : MonoBehaviour
    {
        public const string ResourcePath = "HakoOnna_Ghost";
        /// <summary>和 HouseBlockoutGenerator.FurnitureScale 一致。</summary>
        public const float FurnitureScale = 1.25f;
        /// <summary>容器类型（和 ContainerPrefabBuilder 的 ContainerKind 同名），也是动作名的后缀。</summary>
        public static readonly string[] Kinds =
            { "Oshiire", "YofukuTansu", "ChaTansu", "GetaBako", "Tansu", "Nagamochi", "Tsuzura", "Komebitsu" };

        [Tooltip("Pose_<容器>（藏匿姿势，静止），顺序同 Kinds；现在只留作参考，播的是 Lunge 的第一帧")]
        [SerializeField] AnimationClip[] poses = new AnimationClip[8];
        [Tooltip("Lunge_<容器>：从藏匿姿势扑到抓人的整段动作，顺序同 Kinds")] [SerializeField] AnimationClip[] lunges = new AnimationClip[8];
        [Tooltip("两眼中间，跟着头动，+Z 从脸朝外")] [SerializeField] Transform face;
        [Header("编辑器预览（运行时无效）")]
        [Tooltip("预览哪个容器的姿势（-1 = 按父节点的名字找）")] [SerializeField] int previewKind = -1;
        [Range(0f, 1f)] [SerializeField] float previewLunge;
        [Header("头发、袖子、下摆的甩动（只在运行时）")]
        [Tooltip("关掉 = 只有两个姿势之间的插值")] [SerializeField] bool sway = true;
        [Tooltip("头发每 1/60 秒往静态位置靠近的比例；越小拖得越久")] [Range(.01f, 1f)] [SerializeField] float hairPull = .10f;
        [Tooltip("头发每 1/60 秒损失的速度；越小荡得越久")] [Range(0f, 1f)] [SerializeField] float hairDrag = .12f;
        [Tooltip("袖子和下摆：同上")] [Range(.01f, 1f)] [SerializeField] float clothPull = .16f;
        [Range(0f, 1f)] [SerializeField] float clothDrag = .20f;
        [Tooltip("最多偏离静态位置多少度")] [Range(0f, 90f)] [SerializeField] float swayLimit = 55f;
        [Header("活体细节（只在运行时）")]
        [Tooltip("下巴发颤、头微抖、喘气、手指收紧；关掉 = 停住后完全静止")] [SerializeField] bool alive = true;
        [Tooltip("整体强度")] [Range(0f, 2f)] [SerializeField] float aliveAmount = 1f;

        PlayableGraph graph;
        AnimationClipPlayable clip;
        int kind = -1, builtKind = -1;
        float lunge;

        struct SwayBone
        {
            public Transform t;
            public bool hair;
            public Vector3 axis;          // 骨头朝末端的方向（骨头自己的坐标）
            public float length;          // 到末端的长度（未缩放）
            public Quaternion animLocal;  // 动作给的局部旋转 = 静态垂落
            public Vector3 tail, prev;    // 末端现在和上一步的世界位置
        }

        struct AliveBone
        {
            public Transform t;
            public Quaternion animLocal;  // 动作给的局部旋转
            public Vector3 amp;           // 每个轴抖多少度（Lunge = 1 时）
            public float freq, seed;      // 抖动的快慢；每根骨头不同步
            public float atRest;          // Lunge = 0 时剩多少（0..1）
            public bool jaw;              // 下巴只绕左右轴开合：amp.x 度，轴在 axis 里
            public Vector3 axis;
        }

        const float SwayStep = 1f / 60f;
        AliveBone[] aliveBones;
        SwayBone[] swayBones;
        float swayClock;
        bool swayReset = true;

        public Transform Face => face;
        /// <summary>当前容器类型（Kinds 里的名字），还没挂到容器上时是 null。</summary>
        public string Kind => kind < 0 ? null : Kinds[kind];

        /// <summary>0 = 藏匿姿势，1 = 突脸终点。</summary>
        public float Lunge
        {
            get => lunge;
            set
            {
                lunge = Mathf.Clamp01(value);
                Evaluate();
            }
        }

        /// <summary>生成箱女并挂进这个容器（藏匿姿势）。</summary>
        public static HakoOnnaGhost Spawn(ContainerVisual box)
        {
            var prefab = Resources.Load<GameObject>(ResourcePath);
            if (prefab == null) throw new InvalidOperationException("找不到 Resources/" + ResourcePath);
            var ghost = Instantiate(prefab).GetComponent<HakoOnnaGhost>();
            ghost.name = prefab.name;
            ghost.AttachTo(box);
            return ghost;
        }

        /// <summary>挂到容器下面、换成这个容器的藏匿姿势。容器类型按根节点的名字找（&lt;房间&gt;_&lt;类型&gt;_&lt;序号&gt;、Container_&lt;类型&gt;_A）。</summary>
        public void AttachTo(ContainerVisual box)
        {
            if (box == null) throw new ArgumentNullException(nameof(box));
            var root = box.transform.parent != null ? box.transform.parent : box.transform;
            AttachTo(root, KindIndex(root));
        }

        /// <summary>挂到容器根节点下面；kindIndex 是 Kinds 的下标。</summary>
        public void AttachTo(Transform containerRoot, int kindIndex)
        {
            if (kindIndex < 0 || kindIndex >= Kinds.Length)
                throw new ArgumentException($"{(containerRoot ? containerRoot.name : "null")}：认不出容器类型");
            var anchor = containerRoot;
            if (Kinds[kindIndex] == "Tansu") anchor = FindDeep(containerRoot, "Drawer_0") ?? containerRoot;
            transform.SetParent(anchor, false);
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
            transform.localScale = Vector3.one / FurnitureScale;
            kind = kindIndex;
            lunge = 0f;
            swayReset = true;   // 换了地方：头发从静态位置重新开始
            Evaluate();
        }

        /// <summary>从这个节点往上找名字里的容器类型；找不到返回 -1。</summary>
        public static int KindIndex(Transform t)
        {
            for (; t != null; t = t.parent)
                foreach (var token in t.name.Split('_', ' '))
                {
                    int i = Array.IndexOf(Kinds, token);
                    if (i >= 0) return i;
                }
            return -1;
        }

        static Transform FindDeep(Transform t, string name)
        {
            if (t.name == name) return t;
            foreach (Transform c in t)
            {
                var r = FindDeep(c, name);
                if (r != null) return r;
            }
            return null;
        }

        void Evaluate()
        {
            if (kind < 0 || lunges == null || kind >= lunges.Length || lunges[kind] == null) return;
            if (!graph.IsValid() || builtKind != kind) Build();
            clip.SetTime(lunge * lunges[kind].length);   // Lunge = 这段动作走到哪了
            graph.Evaluate(0f);
            if (swayBones != null)
                for (int i = 0; i < swayBones.Length; i++) swayBones[i].animLocal = swayBones[i].t.localRotation;
            if (aliveBones != null)
                for (int i = 0; i < aliveBones.Length; i++) aliveBones[i].animLocal = aliveBones[i].t.localRotation;
        }

        void LateUpdate()
        {
            if (!Application.isPlaying || kind < 0 || !graph.IsValid()) return;
            if (swayBones == null) BuildSway();
            if (aliveBones == null) BuildAlive();
            if (alive) AlivePass();   // 先动身体，头发和袖子再跟着它甩
            else if (aliveWasOn)
                for (int i = 0; i < aliveBones.Length; i++) aliveBones[i].t.localRotation = aliveBones[i].animLocal;
            aliveWasOn = alive;
            if (!sway || swayBones.Length == 0) return;
            float scale = transform.lossyScale.x;
            if (swayReset)
            {
                swayReset = false;
                swayClock = 0f;
                for (int i = 0; i < swayBones.Length; i++)
                {
                    ref var b = ref swayBones[i];
                    b.t.localRotation = b.animLocal;
                    b.tail = b.prev = b.t.position + b.t.rotation * b.axis * (b.length * scale);
                }
                return;
            }
            swayClock += Mathf.Min(Time.deltaTime, .1f);
            for (int step = 0; step < 4 && swayClock >= SwayStep; step++)
            {
                swayClock -= SwayStep;
                SwayPass(scale, true);
            }
            SwayPass(scale, false);   // 这一帧动作可能刚把骨头写回静态位置，再对准一次
        }

        bool aliveWasOn;

        /// <summary>在动作给的姿势上叠一层小幅度的抖动（柏林噪声，各骨头不同步）。</summary>
        void AlivePass()
        {
            float time = Time.time;
            for (int i = 0; i < aliveBones.Length; i++)
            {
                ref var b = ref aliveBones[i];
                float k = aliveAmount * Mathf.Lerp(b.atRest, 1f, lunge);
                float x = Mathf.PerlinNoise(time * b.freq, b.seed) * 2f - 1f;
                if (b.jaw)
                {
                    b.t.localRotation = b.animLocal * Quaternion.AngleAxis(b.amp.x * x * k, b.axis);
                    continue;
                }
                float y = Mathf.PerlinNoise(time * b.freq, b.seed + 31.7f) * 2f - 1f;
                float z = Mathf.PerlinNoise(time * b.freq, b.seed + 67.3f) * 2f - 1f;
                b.t.localRotation = b.animLocal * Quaternion.Euler(b.amp.x * x * k, b.amp.y * y * k, b.amp.z * z * k);
            }
        }

        void BuildAlive()
        {
            var list = new System.Collections.Generic.List<AliveBone>();
            float seed = 0f;
            void Add(Transform t, Vector3 amp, float freq, float atRest, bool jaw = false)
            {
                if (t == null) return;
                seed += 13.37f;
                var b = new AliveBone { t = t, animLocal = t.localRotation, amp = amp, freq = freq, seed = seed, atRest = atRest, jaw = jaw };
                // 下巴的开合轴 = 脸的左右方向（Face 的 +X），换到下巴自己的坐标里
                if (jaw) b.axis = face != null ? t.InverseTransformDirection(face.right) : Vector3.right;
                list.Add(b);
            }
            foreach (var t in GetComponentsInChildren<Transform>(true))   // 父节点在前
            {
                string n = t.name;
                if (n == "Jaw") Add(t, new Vector3(3.5f, 0f, 0f), 12f, 0f, true);            // 嘴闭着时不能颤开
                else if (n == "Head") Add(t, new Vector3(1.2f, 1.2f, 1.6f), 7f, .3f);
                else if (n == "Neck") Add(t, new Vector3(.8f, .8f, 1f), 5f, .3f);
                else if (n == "UpperChest") Add(t, new Vector3(1.6f, .3f, .3f), 2.2f, .35f);   // 喘
                else if (n == "Spine") Add(t, new Vector3(.8f, .4f, .4f), 1.3f, .35f);
                else if (n.EndsWith("_Hand", StringComparison.Ordinal)) Add(t, new Vector3(1.5f, 1.5f, 1.5f), 5f, .2f);
                else if (n.EndsWith("Proximal", StringComparison.Ordinal) || n.EndsWith("Intermediate", StringComparison.Ordinal))
                    Add(t, new Vector3(2.2f, 2.2f, 2.2f), 6f, .2f);                               // 手指一下下收紧
            }
            aliveBones = list.ToArray();
        }

        /// <summary>父骨头在前：每根骨头对准自己带惯性的末端；integrate = 往前算一步。</summary>
        void SwayPass(float scale, bool integrate)
        {
            for (int i = 0; i < swayBones.Length; i++)
            {
                ref var b = ref swayBones[i];
                var rest = b.t.parent.rotation * b.animLocal;
                Vector3 pos = b.t.position, restDir = rest * b.axis;
                float len = b.length * scale;
                var tail = b.tail;
                if (integrate)
                {
                    float pull = b.hair ? hairPull : clothPull, drag = b.hair ? hairDrag : clothDrag;
                    var next = tail + (tail - b.prev) * (1f - drag) + (pos + restDir * len - tail) * pull;
                    b.prev = tail;
                    tail = next;
                }
                var dir = tail - pos;
                dir = dir.sqrMagnitude < 1e-10f ? restDir : dir.normalized;
                float angle = Vector3.Angle(restDir, dir);
                if (angle > swayLimit) dir = Vector3.Slerp(restDir, dir, swayLimit / angle);
                b.tail = pos + dir * len;
                b.t.rotation = Quaternion.FromToRotation(restDir, dir) * rest;
            }
        }

        void BuildSway()
        {
            var list = new System.Collections.Generic.List<SwayBone>();
            var all = GetComponentsInChildren<Transform>(true);   // 父节点在前
            var axis = Vector3.up;                                // 骨头朝向：从任意一根有子骨头的链骨头量出来
            foreach (var t in all)
                if (IsSwayBone(t, out _) && t.childCount > 0 && t.GetChild(0).localPosition.sqrMagnitude > 1e-8f)
                {
                    axis = t.GetChild(0).localPosition.normalized;
                    break;
                }
            foreach (var t in all)
            {
                if (!IsSwayBone(t, out bool hair)) continue;
                float length = t.childCount > 0 ? t.GetChild(0).localPosition.magnitude
                    : IsSwayBone(t.parent, out _) ? t.localPosition.magnitude : .06f;   // 末端骨头：和上一节一样长
                var a = t.childCount > 0 && length > 1e-4f ? t.GetChild(0).localPosition / length : axis;
                list.Add(new SwayBone { t = t, hair = hair, axis = a, length = Mathf.Max(length, .01f), animLocal = t.localRotation });
            }
            swayBones = list.ToArray();
            swayReset = true;
        }

        static bool IsSwayBone(Transform t, out bool hair)
        {
            hair = t != null && t.name.StartsWith("Hair_", StringComparison.Ordinal);
            return hair || (t != null && (t.name.StartsWith("SleeveBag_", StringComparison.Ordinal)
                                          || t.name.StartsWith("Hem_", StringComparison.Ordinal)));
        }

        void Build()
        {
            if (graph.IsValid()) graph.Destroy();
            graph = PlayableGraph.Create(name + ".Pose");
            graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            var output = AnimationPlayableOutput.Create(graph, "Pose", GetComponent<Animator>());
            clip = AnimationClipPlayable.Create(graph, lunges[kind]);   // 第一帧 = 藏匿姿势（和 Pose_<容器> 相同）
            clip.SetApplyFootIK(false);
            output.SetSourcePlayable(clip);
            builtKind = kind;
        }

        void OnDisable()
        {
            if (graph.IsValid()) graph.Destroy();
            builtKind = -1;
        }

        void OnEnable()
        {
            swayReset = true;
            if (!Application.isPlaying) Preview();
            else Evaluate();
        }

        void OnValidate()
        {
            if (Application.isPlaying || !isActiveAndEnabled) return;
            // OnValidate 里不能改层级和 Animator，下一帧再预览
#if UNITY_EDITOR
            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (this != null && isActiveAndEnabled && !Application.isPlaying) Preview();
            };
#endif
        }

        void Preview()
        {
            int k = previewKind >= 0 && previewKind < Kinds.Length ? previewKind : KindIndex(transform.parent);
            if (k < 0) return;
            kind = k;
            lunge = previewLunge;
            Evaluate();
        }
    }
}
