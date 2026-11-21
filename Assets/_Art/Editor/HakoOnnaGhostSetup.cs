using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace HakoOnna.Art.EditorTools
{
    /// <summary>
    /// 箱女（开箱突脸用）：模型导入设置、材质、预制体，和放进 8 种容器的检查截图。
    /// <list type="bullet">
    /// <item>模型：Models/Characters/HakoOnna/HakoOnna.fbx，由 Blender 脚本导出（Tools~/ghost_pose_lib.py … --fbx），
    ///   16 个动作：Pose_&lt;容器&gt;（藏匿姿势）、Lunge_&lt;容器&gt;（突脸终点），Generic 骨骼。</item>
    /// <item>贴图：Tripo 原模型带的 4K 贴图（Models/External/Tripo/GhostGirl/*.fbm），突脸时脸占满屏幕，所以不压到 2K。</item>
    /// <item>预制体：Resources/HakoOnna_Ghost（HakoOnnaGhost 组件，程序用法见那个类的注释）。</item>
    /// <item>检查：Unity -batchmode -executeMethod HakoOnna.Art.EditorTools.HakoOnnaGhostSetup.SetupAndCheck -quit
    ///   [-ghostCheckOut 目录]，每种容器出藏匿 / 突脸 / 镜头拉到 0.2 m 三张图，和每根骨头在容器坐标里的位置（bones.json）。</item>
    /// </list>
    /// </summary>
    internal static class HakoOnnaGhostSetup
    {
        public const string ModelPath = "Assets/_Art/Models/Characters/HakoOnna/HakoOnna.fbx";
        public const string TexturePath = "Assets/_Art/Models/External/Tripo/GhostGirl/tripo_convert_48543c68-2071-4064-a813-fbffebcd98c8.fbm/tripo_0bc416b6_29b3_48bd_9686_3db4dd63ea80.jpg";
        public const string MaterialPath = "Assets/_Art/Materials/Characters/M_HakoOnna.mat";
        public const string PrefabPath = "Assets/_Art/Resources/" + HakoOnnaGhost.ResourcePath + ".prefab";
        /// <summary>FBX 里的材质名（Tripo 原名）。</summary>
        const string SourceMaterial = "tripo_mat_48543c68";
        const string RevTag = "HakoArtRev", Rev = "ghost2";
        const string DefaultCheckDir = "Docs/screenshots/hakoonna";

        [MenuItem("Hako/Art/箱女：材质和预制体", priority = 40)]
        public static void Setup()
        {
            var imp = AssetImporter.GetAtPath(ModelPath) as ModelImporter;
            if (imp == null) throw new InvalidOperationException("缺少 " + ModelPath + "（先用 Blender 脚本导出）");
            if (imp.animationType != ModelImporterAnimationType.Generic || imp.clipAnimations.Length == 0)
                AssetDatabase.ImportAsset(ModelPath, ImportAssetOptions.ForceUpdate);   // 第一次导入时后处理器还没编译

            var tex = EnsureTexture();
            var mat = EnsureMaterial(tex);
            var id = new AssetImporter.SourceAssetIdentifier(typeof(Material), SourceMaterial);
            imp = (ModelImporter)AssetImporter.GetAtPath(ModelPath);
            if (!imp.GetExternalObjectMap().TryGetValue(id, out var mapped) || mapped != mat)
            {
                imp.AddRemap(id, mat);
                imp.SaveAndReimport();
            }
            BuildPrefab(mat);
            AssetDatabase.SaveAssets();
            var clips = Clips();
            Debug.Log($"[Hako Ghost] 预制体 {PrefabPath}：{clips.Count} 个动作（{string.Join(", ", clips.Keys.OrderBy(k => k))}）");
        }

        /// <summary>命令行入口：建材质、预制体，再出检查图。</summary>
        public static void SetupAndCheck()
        {
            Setup();
            Check(ArgValue("-ghostCheckOut") ?? DefaultCheckDir);
        }

        [MenuItem("Hako/Art/箱女：8 种容器检查截图", priority = 42)]
        static void MenuCheck()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            string prev = EditorSceneManager.GetActiveScene().path;
            try { Check(DefaultCheckDir); }
            finally
            {
                if (!string.IsNullOrEmpty(prev)) EditorSceneManager.OpenScene(prev);
            }
            EditorUtility.RevealInFinder(DefaultCheckDir);
        }

        /// <summary>编辑器里看她在某个容器里：选中场景里的容器，生成一个不会存进场景的箱女（拖她身上的 Preview Lunge 看突脸）。</summary>
        [MenuItem("Hako/Art/箱女：放进选中的容器（预览，不存进场景）", priority = 41)]
        static void PreviewInSelected()
        {
            var sel = Selection.activeTransform;
            ContainerVisual box = null;
            if (sel != null) box = sel.GetComponentInParent<ContainerVisual>();
            if (box == null && sel != null) box = sel.GetComponentInChildren<ContainerVisual>();
            if (box == null)
            {
                EditorUtility.DisplayDialog("箱女预览", "先在场景里选中一个容器（Containers 下面的 <房间>_<类型>_<序号>）。", "好");
                return;
            }
            var ghost = HakoOnnaGhost.Spawn(box);
            foreach (var t in ghost.GetComponentsInChildren<Transform>(true))
                t.gameObject.hideFlags = HideFlags.DontSaveInEditor;
            Selection.activeGameObject = ghost.gameObject;
            Debug.Log($"[Hako Ghost] 预览：{box.transform.parent.name}（{ghost.Kind}）。容器 Visual 的 Editor Preview 拖到 1 打开，" +
                      "箱女的 Preview Lunge 拖到 1 是突脸终点。不会存进场景，看完删掉就行。");
        }

        // ------------------------------------------------------------------ 资产

        static Texture2D EnsureTexture()
        {
            var ti = (TextureImporter)AssetImporter.GetAtPath(TexturePath);
            bool dirty = false;
            if (ti.maxTextureSize < 4096)
            {
                ti.maxTextureSize = 4096;
                dirty = true;
            }
            foreach (var platform in new[] { "Standalone" })
            {
                var ps = ti.GetPlatformTextureSettings(platform);
                if (ps.overridden && ps.maxTextureSize < 4096)
                {
                    ps.maxTextureSize = 4096;
                    ti.SetPlatformTextureSettings(ps);
                    dirty = true;
                }
            }
            if (dirty) ti.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
        }

        /// <summary>
        /// URP Lit，只画正面：和服、袖子、头发是单层面片，导出时已经加了反过来的背面（ghost_pose_lib.two_sided_copy）。
        /// 不用 Cull Off：URP Lit 画背面时用的还是正面的法线，袖子里侧在手电下是黑的。
        /// 只建一次，之后在 Unity 里手调的不覆盖；想重来就换 Rev。
        /// </summary>
        static Material EnsureMaterial(Texture2D tex)
        {
            ArtKit.EnsureFolder(Path.GetDirectoryName(MaterialPath).Replace('\\', '/'));
            var m = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            bool created = m == null;
            if (created) m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = Path.GetFileNameWithoutExtension(MaterialPath) };
            else if (m.GetTag(RevTag, false, "") == Rev) return m;
            m.SetTexture("_BaseMap", tex);
            m.SetColor("_BaseColor", Color.white);
            m.SetFloat("_WorkflowMode", 1f);
            m.SetFloat("_Metallic", 0f);
            m.SetFloat("_Smoothness", .3f);
            m.SetFloat("_SmoothnessTextureChannel", 0f);
            m.SetFloat("_Surface", 0f);
            m.SetFloat("_AlphaClip", 0f);
            m.DisableKeyword("_ALPHATEST_ON");
            m.SetFloat("_Cull", (float)CullMode.Back);
            m.doubleSidedGI = false;
            m.SetOverrideTag("RenderType", "Opaque");
            m.renderQueue = -1;
            m.SetOverrideTag(RevTag, Rev);
            if (created) AssetDatabase.CreateAsset(m, MaterialPath);
            else EditorUtility.SetDirty(m);
            return m;
        }

        static Dictionary<string, AnimationClip> Clips() =>
            AssetDatabase.LoadAllAssetsAtPath(ModelPath).OfType<AnimationClip>()
                .Where(c => !c.name.StartsWith("__preview__"))
                .GroupBy(c => c.name).ToDictionary(g => g.Key, g => g.First());

        static void BuildPrefab(Material mat)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            var go = (GameObject)PrefabUtility.InstantiatePrefab(model);
            try
            {
                go.name = HakoOnnaGhost.ResourcePath;
                var animator = go.GetComponent<Animator>();
                if (animator == null) animator = go.AddComponent<Animator>();
                animator.runtimeAnimatorController = null;
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;   // 镜头跟着 Face 走，看不见时也要更新
                foreach (var smr in go.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    smr.sharedMaterial = mat;
                    smr.updateWhenOffscreen = true;   // 姿势离静止姿态很远（倒挂、折叠），默认包围盒会被错误剔除
                }

                // 脸：两眼中间，跟着头动；+Z = 静止姿态（蒙皮绑定姿态，她站直朝前）下从头骨指向两眼的水平方向。
                // 不用模型的默认姿态：Unity 拿第一个动作（Lunge_ChaTansu）的第一帧当默认姿态（2026-11-21 查过），头是歪的。
                var root = go.transform;
                Transform head = Find(root, "Head"), le = Find(root, "Left_Eye"), re = Find(root, "Right_Eye");
                var skin = go.GetComponentInChildren<SkinnedMeshRenderer>(true);
                int ih = Array.IndexOf(skin.bones, head);
                if (ih < 0) throw new InvalidOperationException("Head 不在蒙皮骨骼里");
                var headRest = root.worldToLocalMatrix * skin.transform.localToWorldMatrix * skin.sharedMesh.bindposes[ih].inverse;
                var eyeLocal = head.InverseTransformPoint((le.position + re.position) * .5f);   // 眼睛长在头上，和姿势无关
                var fwd = Vector3.ProjectOnPlane(headRest.MultiplyPoint3x4(eyeLocal) - (Vector3)headRest.GetColumn(3), Vector3.up).normalized;
                var face = new GameObject("Face").transform;
                face.SetParent(head, false);
                face.localPosition = eyeLocal;
                face.localRotation = Quaternion.Inverse(headRest.rotation) * Quaternion.LookRotation(fwd, Vector3.up);

                var ghost = go.GetComponent<HakoOnnaGhost>();
                if (ghost == null) ghost = go.AddComponent<HakoOnnaGhost>();
                var clips = Clips();
                var so = new SerializedObject(ghost);
                var poses = so.FindProperty("poses");
                var lunges = so.FindProperty("lunges");
                poses.arraySize = lunges.arraySize = HakoOnnaGhost.Kinds.Length;
                for (int i = 0; i < HakoOnnaGhost.Kinds.Length; i++)
                {
                    string k = HakoOnnaGhost.Kinds[i];
                    if (!clips.TryGetValue("Pose_" + k, out var p) || !clips.TryGetValue("Lunge_" + k, out var l))
                        throw new InvalidOperationException($"{ModelPath} 里缺少 Pose_{k} / Lunge_{k}");
                    poses.GetArrayElementAtIndex(i).objectReferenceValue = p;
                    lunges.GetArrayElementAtIndex(i).objectReferenceValue = l;
                }
                so.FindProperty("face").objectReferenceValue = face;
                so.ApplyModifiedPropertiesWithoutUndo();

                ArtKit.EnsureFolder(Path.GetDirectoryName(PrefabPath).Replace('\\', '/'));
                PrefabUtility.SaveAsPrefabAsset(go, PrefabPath);
                Debug.Log($"[Hako Ghost] 层级：{string.Join(" | ", root.Cast<Transform>().Select(c => $"{c.name} pos {c.localPosition} rot {c.localRotation} scale {c.localScale}"))}；" +
                          $"静止姿态的脸 {headRest.MultiplyPoint3x4(eyeLocal)}，朝 {fwd}；模型默认姿态的脸 {(le.position + re.position) * .5f}");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        static Transform Find(Transform t, string name)
        {
            if (t.name == name) return t;
            foreach (Transform c in t)
            {
                var r = Find(c, name);
                if (r != null) return r;
            }
            return null;
        }

        // ------------------------------------------------------------------ 检查截图

        const float EyeHeight = 1.58f, Fov = 75f, Board = .03f;

        /// <summary>
        /// 和 Tools~/pose_reference_boxes.py 的 PlayerEye 一样：玩家打开容器后检视镜头停的位置，对准美术设定 6.2 的脸的位置。
        /// 坐标是容器根节点的本地坐标乘 1.25（房子里的米）。
        /// </summary>
        static (Vector3 pos, Quaternion rot) PlayerEye(ContainerKind kind)
        {
            const float S = HakoOnnaGhost.FurnitureScale, K = Board;
            var size = ContainerPrefabBuilder.Sizes[kind];
            float w = size.x, h = size.y, d = size.z;
            Vector3 anchor, face;
            string mode = "door";
            switch (kind)
            {
                case ContainerKind.Oshiire:
                    anchor = S * new Vector3(w / 4, h * .5f + K, 0);
                    face = new Vector3(S * w / 4, 1.70f, S * d / 2 - .25f);
                    break;
                case ContainerKind.YofukuTansu:
                case ContainerKind.ChaTansu:
                case ContainerKind.GetaBako:
                    anchor = S * new Vector3(0, K + .01f, 0);
                    face = kind == ContainerKind.YofukuTansu ? new Vector3(0, 1.20f, S * d / 2 - .25f)
                        : kind == ContainerKind.ChaTansu ? new Vector3(0, .60f, S * d / 2 - .20f)
                        : new Vector3(0, 1.50f, S * d / 2 - .18f);
                    break;
                case ContainerKind.Tansu:
                {
                    float dh = (h - 2 * K) / 3, yBottom = h - K - dh;
                    var origin = S * new Vector3(0, yBottom, d * .75f);
                    anchor = origin + S * new Vector3(0, .02f, 0);
                    face = origin + new Vector3(0, S * .014f + .13f, S * d / 2 - .12f);
                    mode = "drawer";
                    break;
                }
                default:
                    anchor = S * new Vector3(0, K + .01f, 0);
                    face = kind == ContainerKind.Nagamochi ? new Vector3(0, S * (K + .0055f) + .30f, -.15f)
                        : kind == ContainerKind.Tsuzura ? new Vector3(0, .50f, 0)
                        : new Vector3(0, S * h - .10f, 0);
                    mode = "lid";
                    break;
            }
            var eye = new Vector3(0, EyeHeight, S * (d / 2 + .6f));
            var off = anchor + new Vector3(0, .08f, 0) - eye;
            float dist = off.magnitude;
            var dest = eye + off * (Mathf.Clamp(dist - .5f, 0f, .48f) / dist);
            if (mode != "door")
            {
                float y = mode == "drawer" ? Mathf.Max(EyeHeight - .25f, anchor.y + .72f) : Mathf.Max(EyeHeight, S * h + .45f);
                dest = new Vector3(anchor.x, y, anchor.z + .17f);
            }
            var f = (face - dest).normalized;
            var up = Mathf.Abs(f.y) < .9f ? Vector3.up : Vector3.back;   // 往下看时，画面上方是容器的里侧
            return (dest, Quaternion.LookRotation(f, up));
        }

        public static void Check(string outDir)
        {
            Directory.CreateDirectory(outDir);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.fog = false;
            RenderSettings.skybox = null;   // 和老宅一样没有天空：不然环境反射是默认的蓝天
            var ambient = new Color(.42f, .42f, .44f);
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = ambient;
            var sh = new SphericalHarmonicsL2();
            sh.AddAmbientLight(ambient);
            RenderSettings.ambientProbe = sh;

            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.transform.localScale = new Vector3(.4f, 1f, .4f);
            var key = new GameObject("Key").AddComponent<Light>();
            key.type = LightType.Directional;
            key.intensity = 1.1f;
            key.shadows = LightShadows.Soft;
            var cam = new GameObject("CheckCam").AddComponent<Camera>();
            cam.fieldOfView = Fov;
            cam.nearClipPlane = .01f;
            cam.farClipPlane = 50f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(.16f, .16f, .18f);

            var json = new StringBuilder("{\n");
            bool firstEntry = true;
            foreach (var name in HakoOnnaGhost.Kinds)
            {
                var kind = (ContainerKind)Enum.Parse(typeof(ContainerKind), name);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ContainerPrefabBuilder.PathFor(kind, "A"));
                var box = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                box.transform.localScale = Vector3.one * HakoOnnaGhost.FurnitureScale;
                var visual = box.GetComponentInChildren<ContainerVisual>();
                visual.SetOpenImmediate(true);
                var (eyePos, eyeRot) = PlayerEye(kind);
                Vector3 camPos = box.transform.TransformPoint(eyePos / HakoOnnaGhost.FurnitureScale);
                var camRot = box.transform.rotation * eyeRot;

                foreach (var (clip, lunge) in new[] { ("Pose_" + name, 0f), ("Lunge_" + name, 1f) })
                {
                    // 每个姿势用新的实例：批处理里不跑帧循环，蒙皮只在实例第一次渲染时算，之后换姿势画出来的还是旧的
                    var ghost = HakoOnnaGhost.Spawn(visual);
                    var anchor = ghost.transform.parent;
                    ghost.Lunge = lunge;
                    Shoot(cam, key, camPos, camRot, Path.Combine(outDir, $"{name}_{(lunge > 0 ? "lunge" : "pose")}.png"));
                    if (!firstEntry) json.Append(",\n");
                    firstEntry = false;
                    json.Append($"  \"{clip}\": {{\"anchor\": \"{anchor.name}\"");
                    foreach (var t in ghost.GetComponentsInChildren<Transform>(true))
                    {
                        if (t == ghost.transform || t.GetComponent<Renderer>() != null) continue;
                        var p = anchor.InverseTransformPoint(t.position) * HakoOnnaGhost.FurnitureScale;
                        json.Append(string.Format(CultureInfo.InvariantCulture, ", \"{0}\": [{1:F5}, {2:F5}, {3:F5}]", t.name, p.x, p.y, p.z));
                    }
                    json.Append("}");

                    if (lunge > 0)
                    {
                        // 突脸终点：镜头沿着脸 → 原镜头的连线拉到离两眼 0.2 m（美术设定 4.3）；姿势没变，同一个实例可以再拍
                        Transform le = Find(ghost.transform, "Left_Eye"), re = Find(ghost.transform, "Right_Eye");
                        var eyes = (le.position + re.position) * .5f;
                        var pulled = eyes + (camPos - eyes).normalized * .2f;
                        var pulledRot = Quaternion.FromToRotation(camRot * Vector3.forward, eyes - pulled) * camRot;
                        Shoot(cam, key, pulled, pulledRot, Path.Combine(outDir, $"{name}_climax.png"));
                        Debug.Log(string.Format(CultureInfo.InvariantCulture, "[Hako Ghost] {0}: 挂在 {1}，眼睛离检视镜头 {2:F3} m",
                            name, anchor.name, Vector3.Distance(eyes, camPos)));
                    }
                    Object.DestroyImmediate(ghost.gameObject);
                }
                Object.DestroyImmediate(box);
            }
            json.Append("\n}\n");
            File.WriteAllText(Path.Combine(outDir, "bones.json"), json.ToString());
            Debug.Log($"[Hako Ghost] 检查图和 bones.json → {outDir}");
        }

        static void Shoot(Camera cam, Light key, Vector3 pos, Quaternion rot, string file)
        {
            cam.transform.SetPositionAndRotation(pos, rot);
            key.transform.rotation = Quaternion.LookRotation(rot * Vector3.forward + Vector3.down * .35f);
            const int w = 960, h = 540;
            var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            rt.Create();
            cam.targetTexture = rt;
            var req = new RenderPipeline.StandardRequest { destination = rt };
            if (RenderPipeline.SupportsRenderRequest(cam, req)) RenderPipeline.SubmitRenderRequest(cam, req);
            else cam.Render();
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            File.WriteAllBytes(file, tex.EncodeToPNG());
            cam.targetTexture = null;
            Object.DestroyImmediate(tex);
            rt.Release();
            Object.DestroyImmediate(rt);
        }

        static string ArgValue(string flag)
        {
            var args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, flag);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
        }
    }

    /// <summary>箱女模型的导入设置（每次导入都按这里来；动作名去掉 Blender 的“Armature|”前缀）。</summary>
    internal sealed class HakoOnnaGhostImport : AssetPostprocessor
    {
        void OnPreprocessModel()
        {
            if (assetPath != HakoOnnaGhostSetup.ModelPath) return;
            var imp = (ModelImporter)assetImporter;
            imp.importCameras = false;
            imp.importLights = false;
            imp.importVisibility = false;
            imp.importBlendShapes = false;
            imp.addCollider = false;
            imp.isReadable = false;
            imp.meshCompression = ModelImporterMeshCompression.Off;
            imp.importNormals = ModelImporterNormals.Import;
            imp.importTangents = ModelImporterTangents.None;   // 只有颜色贴图，没有法线贴图
            imp.useFileScale = true;
            imp.globalScale = 1f;
            imp.bakeAxisConversion = true;
            imp.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
            imp.materialLocation = ModelImporterMaterialLocation.InPrefab;
            imp.animationType = ModelImporterAnimationType.Generic;
            imp.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            imp.optimizeGameObjects = false;   // HakoOnnaGhost 要找 Head / 眼睛，程序要跟 Face
            imp.motionNodeName = "";
            imp.importAnimation = true;
            imp.resampleCurves = true;
            imp.animationCompression = ModelImporterAnimationCompression.Off;   // 静态姿势，压缩会让手指、头发偏几毫米
        }

        void OnPreprocessAnimation()
        {
            if (assetPath != HakoOnnaGhostSetup.ModelPath) return;
            var imp = (ModelImporter)assetImporter;
            var clips = new List<ModelImporterClipAnimation>();
            foreach (var c in imp.defaultClipAnimations)
            {
                string n = c.takeName.Substring(c.takeName.LastIndexOf('|') + 1);
                if (!n.StartsWith("Pose_") && !n.StartsWith("Lunge_")) continue;
                c.name = n;
                c.loopTime = false;
                c.lockRootRotation = c.lockRootHeightY = c.lockRootPositionXZ = false;
                clips.Add(c);
            }
            imp.clipAnimations = clips.ToArray();
        }
    }
}
