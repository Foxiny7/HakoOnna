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
    ///   ghost.Lunge = t;                        // 0 = 藏匿姿势，1 = 突脸终点（双手抓住探索者的头）；时间曲线程序定
    ///   Destroy(ghost.gameObject);              // 演出结束
    /// Face：两眼中间，跟着头动，+Z 从脸朝外。
    ///
    /// 姿势是在“家具放大 1.25 倍”的容器里摆的，所以挂在容器根节点下（箪笥挂在 Drawer_0 下，跟着抽屉动），
    /// 位置 0、不旋转、缩放 1/1.25；场景里的容器放大 1.25 倍后，她正好 1.2 m 高。
    /// 0 到 1 之间目前是两个姿势直接插值（中间“骨头掰回原位”的过渡帧还没做）。
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

        [Tooltip("Pose_<容器>，顺序同 Kinds")] [SerializeField] AnimationClip[] poses = new AnimationClip[8];
        [Tooltip("Lunge_<容器>，顺序同 Kinds")] [SerializeField] AnimationClip[] lunges = new AnimationClip[8];
        [Tooltip("两眼中间，跟着头动，+Z 从脸朝外")] [SerializeField] Transform face;
        [Header("编辑器预览（运行时无效）")]
        [Tooltip("预览哪个容器的姿势（-1 = 按父节点的名字找）")] [SerializeField] int previewKind = -1;
        [Range(0f, 1f)] [SerializeField] float previewLunge;

        PlayableGraph graph;
        AnimationMixerPlayable mixer;
        int kind = -1, builtKind = -1;
        float lunge;

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
            if (kind < 0 || poses == null || lunges == null || kind >= poses.Length || kind >= lunges.Length) return;
            if (poses[kind] == null || lunges[kind] == null) return;
            if (!graph.IsValid() || builtKind != kind) Build();
            mixer.SetInputWeight(0, 1f - lunge);
            mixer.SetInputWeight(1, lunge);
            graph.Evaluate(0f);
        }

        void Build()
        {
            if (graph.IsValid()) graph.Destroy();
            graph = PlayableGraph.Create(name + ".Pose");
            graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            var output = AnimationPlayableOutput.Create(graph, "Pose", GetComponent<Animator>());
            mixer = AnimationMixerPlayable.Create(graph, 2);
            var a = AnimationClipPlayable.Create(graph, poses[kind]);
            var b = AnimationClipPlayable.Create(graph, lunges[kind]);
            a.SetApplyFootIK(false);
            b.SetApplyFootIK(false);
            graph.Connect(a, 0, mixer, 0);
            graph.Connect(b, 0, mixer, 1);
            output.SetSourcePlayable(mixer);
            builtKind = kind;
        }

        void OnDisable()
        {
            if (graph.IsValid()) graph.Destroy();
            builtKind = -1;
        }

        void OnEnable()
        {
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
