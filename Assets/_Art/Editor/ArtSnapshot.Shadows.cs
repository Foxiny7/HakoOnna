using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace HakoOnna.Art.EditorTools
{
    /// <summary>
    /// 阴影检查（2026-11-26）：照探索者在游戏里看到的样子拍——相机和 Explorer.prefab 一样（75°、近裁 0.04、不开 HDR、清屏用天空盒），
    /// 手电不在相机正中，而是和 NetworkPlayer.Inspection.cs 一样在眼睛的左下前方（-0.17, -0.19, 0.47），所以手电的影子拍得到
    /// （Mood / MoodX 机位的手电就在镜头上，影子全被物体自己挡住）。每个机位拍“开手电 / 关手电”两张。
    /// 不重新生成场景，直接打开存好的 House_Blockout。截图在 Docs/screenshots/shadows/&lt;组名&gt;_&lt;机位&gt;_on|off.jpg。
    /// 命令行：Unity -batchmode -executeMethod HakoOnna.Art.EditorTools.ArtSnapshot.ShootShadowCheck -quit
    ///   可选 -hakoShadowOut 目录：截图改存到这里（试参数时用，别把试验图堆进 Docs）。
    ///   可选 -hakoShadowSets 文件：每行一组“组名 键=值 键=值…”，临时改参数各拍一遍（只改内存，不存盘），用来比较方案。
    ///   键：ssaoI / ssaoR / ssaoD / ssaoS / ssaoF（环境光遮蔽的强度、半径、直射光下的强度、采样档、衰减距离）、
    ///   lm（房间微光投不投影 0/1）、lmStr / lmNear / lmTier、dist（阴影距离）、casc / split2 / split3（级联数和分割）、
    ///   res（月光阴影图）、ares（其它灯的阴影图集）、soft（软阴影档 1–3）、opaque（要不要拷贝不透明画面 0/1）、
    ///   ssaoDown / ssaoB（环境光遮蔽半分辨率、模糊档）、beamRange（手电射程）、
    ///   time（N = 每个机位连画 N 帧计时；单次误差约 1 ms，同一组在文件里多写几遍、取中位数才可靠）、shots（0 = 只计时不出图）、
    ///   moonStr（月光阴影强度）、expo（后处理曝光，调高几档只为看清暗处的影子）、
    ///   beam（手电强度）、beamPos（手电相对眼睛的位置 x:y:z）、aim（1 = 手电照向视线正中看到的那一点，不和视线平行）、
    ///   arms（1 = 加上第一人称手臂和手电筒的替身，看会不会挡光）、
    ///   only（只拍这几个机位，逗号分隔）、states（on / off，只拍开或关手电的那张）、ghost（1 = 箱女视角，照 GhostSight.cs）、
    ///   face（1 = 不拍机位，只拍开箱突脸的两张特写）。
    /// </summary>
    public static partial class ArtSnapshot
    {
        const string ShadowOutDir = "Docs/screenshots/shadows";
        const string ShadowScenePath = "Assets/_Art/Scenes/House_Blockout.unity";
        const string ShadowRendererPath = "Assets/Settings/PC_Renderer.asset";

        /// <summary>手电相对眼睛的位置：灯身 (-.17, -.19, .36) + 灯头往前 .11（NetworkPlayer.Inspection.cs）。</summary>
        static readonly Vector3 GameBeamOffset = new Vector3(-.17f, -.19f, .47f);

        struct ShadowView
        {
            public string name;
            public Vector3 pos, look;
            public ShadowView(string n, Vector3 p, Vector3 l) { name = n; pos = p; look = l; }
        }

        static IEnumerable<ShadowView> ShadowViews()
        {
            // 室内：和同名的 Interior / MoodX 机位一样（V3 旧坐标，跟着房间搬家）
            var indoor = new (string name, Vector3 pos, Vector3 look)[]
            {
                ("genkan", new Vector3(1.2f, 1.55f, 1.2f), new Vector3(7f, 1.0f, 6f)),
                ("daidokoro", new Vector3(6.6f, 1.55f, 8.0f), new Vector3(1.0f, 1.0f, 13.0f)),
                ("ima", new Vector3(12.2f, 2.1f, 7.6f), new Vector3(8.5f, 1.5f, 12.0f)),
                ("zashiki", new Vector3(22.8f, 2.1f, 3.0f), new Vector3(19.5f, 1.2f, 6.8f)),
                ("nando", new Vector3(17.5f, 2.1f, 8.0f), new Vector3(14.6f, 1.0f, 11.6f)),
                ("engawa", new Vector3(8.3f, 2.1f, 1.0f), new Vector3(24f, 1.3f, 1.0f)),
                ("furo", new Vector3(11.0f, 2.1f, 15.2f), new Vector3(16.5f, 1.2f, 19.8f)),
                ("kura", new Vector3(8.4f, 1.85f, 25.0f), new Vector3(14.5f, 1.0f, 29.8f)),
                ("shinshitsu", new Vector3(12.3f, 4.6f, 11.7f), new Vector3(6.0f, 3.6f, 8.0f)),
                ("kodomo", new Vector3(12.2f, 4.6f, 18.1f), new Vector3(16.6f, 3.6f, 14.7f)),
            };
            foreach (var v in indoor)
                yield return new ShadowView(v.name, HouseBlockoutGenerator.MapOldPoint(v.pos), HouseBlockoutGenerator.MapOldPoint(v.look));

            // 院子：主庭园（往縁側看）、门前、后院（旧坐标 × 室外倍数）
            float k = ExteriorScale;
            yield return new ShadowView("garden", new Vector3(17f, 1.6f, -6.2f) * k, new Vector3(16f, 1.8f, 1f) * k);
            yield return new ShadowView("forecourt", new Vector3(2.12f, 1.17f, -6.72f) * k, new Vector3(6.36f, 1.06f, -.71f) * k);
            yield return new ShadowView("backyard", new Vector3(1.06f, 1.2f, 22.27f) * k, new Vector3(4.6f, .85f, 16.6f) * k);

            // 六间大房间的微光：站在灯下那圈光的斜外面 3.2 m，看灯下的地面和家具
            var landmarks = GameObject.Find("House_Blockout/Lighting/Landmarks");
            if (landmarks == null) yield break;
            foreach (Transform t in landmarks.transform)
            {
                Vector3 foot = t.position;
                if (Physics.Raycast(t.position, Vector3.down, out var hit, 4f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) foot = hit.point;
                else foot.y -= 2.3f;
                Vector3 eye = foot + new Vector3(-2.3f, 1.5f, -2.3f);
                for (int i = 0; i < 4; i++)
                {
                    float a = (45f + i * 90f) * Mathf.Deg2Rad;
                    var c = foot + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 3.2f + Vector3.up * 1.5f;
                    if (Physics.CheckSphere(c, .25f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) continue;
                    if (Physics.Linecast(c, foot + Vector3.up * .9f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) continue;
                    eye = c;
                    break;
                }
                yield return new ShadowView("lm_" + t.name.Replace("Landmark_", "").ToLowerInvariant(), eye, foot + Vector3.up * .35f);
            }
        }

        /// <summary>重新生成场景再拍。命令行：Unity -batchmode -executeMethod HakoOnna.Art.EditorTools.ArtSnapshot.GenerateAndShootShadows -quit</summary>
        public static void GenerateAndShootShadows()
        {
            HouseBlockoutGenerator.Generate();
            ShootShadowCheck();
        }

        [MenuItem("Hako/Art/9f 阴影检查（游戏里的光，开 / 关手电）", priority = 95)]
        public static void ShootShadowCheck()
        {
            if (GameObject.Find("House_Blockout") == null) EditorSceneManager.OpenScene(ShadowScenePath, OpenSceneMode.Single);
            Physics.SyncTransforms();
            string outDir = ShadowArg("-hakoShadowOut");
            if (outDir.Length == 0) outDir = ShadowOutDir;
            Directory.CreateDirectory(outDir);

            var sets = new List<(string tag, Dictionary<string, string> kv)>();
            string file = ShadowArg("-hakoShadowSets");
            if (file.Length > 0 && File.Exists(file))
            {
                foreach (var raw in File.ReadAllLines(file))
                {
                    var parts = raw.Trim().Split(new[] { ' ', '\t' }, System.StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length == 0 || parts[0].StartsWith("#")) continue;
                    var kv = new Dictionary<string, string>();
                    for (int i = 1; i < parts.Length; i++)
                    {
                        int eq = parts[i].IndexOf('=');
                        if (eq > 0) kv[parts[i].Substring(0, eq)] = parts[i].Substring(eq + 1);
                    }
                    sets.Add((parts[0], kv));
                }
            }
            if (sets.Count == 0) sets.Add(("now", new Dictionary<string, string>()));

            var views = new List<ShadowView>(ShadowViews());
            var dust = Object.FindObjectsByType<ParticleSystem>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            foreach (var ps in dust) ps.Simulate(14f, true, true, true);

            var camGo = new GameObject("ShadowCheckCam");
            var cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = 75f;
            cam.nearClipPlane = .04f;
            cam.farClipPlane = 1000f;
            cam.allowHDR = false;
            cam.clearFlags = CameraClearFlags.Skybox;
            var data = cam.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true;
            var ghostGo = new GameObject("ShadowCheckGhostLight");
            ghostGo.transform.SetParent(camGo.transform, false);
            var ghostLight = ghostGo.AddComponent<Light>();
            ghostLight.type = LightType.Point;
            ghostLight.color = GhostLightColor;
            ghostLight.range = 20f;
            ghostLight.intensity = 5f;
            ghostLight.enabled = false;
            var beamGo = new GameObject("ShadowCheckBeam");
            beamGo.transform.SetParent(camGo.transform, false);
            var beam = beamGo.AddComponent<Light>();
            beam.type = LightType.Spot;
            beam.range = 30f;
            beam.spotAngle = 70f;
            beam.innerSpotAngle = 35f;
            beam.color = new Color(1f, .95f, .84f);
            beam.shadows = LightShadows.Soft;
            var arms = BuildArmProxies(camGo.transform);

            int count = 0;
            try
            {
                foreach (var (tag, kv) in sets)
                {
                    var undo = new List<System.Action>();
                    try
                    {
                        ApplyShadowOverrides(kv, undo);
                        beam.intensity = kv.TryGetValue("beam", out var bi) ? F(bi) : GameBeamIntensity;
                        beam.range = kv.TryGetValue("beamRange", out var br) ? F(br) : 30f;
                        beamGo.transform.localPosition = kv.TryGetValue("beamPos", out var bp) ? V3(bp) : GameBeamOffset;
                        arms.SetActive(kv.TryGetValue("arms", out var ar) && ar == "1");
                        // 手电筒的替身跟着灯头走（灯头在灯身前面 .11）
                        arms.transform.Find("Flashlight").localPosition = beamGo.transform.localPosition - Vector3.forward * .11f;
                        string[] only = kv.TryGetValue("only", out var o) ? o.Split(',') : null;
                        kv.TryGetValue("states", out var states);
                        // ghost=1：箱女视角（GhostSight.cs）——关雾、关后处理、环境光 (.66, .92, .84)、相机上一盏点光；只拍一张（没有手电）
                        bool ghost = kv.TryGetValue("ghost", out var gh) && gh == "1";
                        ghostLight.enabled = ghost;
                        if (ghost)
                        {
                            bool oldFog = RenderSettings.fog;
                            var oldMode = RenderSettings.ambientMode;
                            var oldAmbient = RenderSettings.ambientLight;
                            var oldProbe = RenderSettings.ambientProbe;
                            RenderSettings.fog = false;
                            SetAmbient(GhostAmbient);
                            data.renderPostProcessing = false;
                            states = "off";
                            undo.Add(() =>
                            {
                                RenderSettings.fog = oldFog;
                                RenderSettings.ambientMode = oldMode;
                                RenderSettings.ambientLight = oldAmbient;
                                RenderSettings.ambientProbe = oldProbe;
                                data.renderPostProcessing = true;
                                ghostLight.enabled = false;
                            });
                        }
                        if (kv.TryGetValue("face", out var fc) && fc == "1")
                        {
                            beam.enabled = false;
                            count += ShootGhostFace(cam, outDir, tag);
                            continue;
                        }
                        bool noShots = kv.TryGetValue("shots", out var sh) && sh == "0"; // shots=0：这一组只计时、不出图
                        foreach (var v in views)
                        {
                            if (noShots) break;
                            if (only != null && System.Array.IndexOf(only, v.name) < 0) continue;
                            camGo.transform.SetPositionAndRotation(v.pos, Quaternion.LookRotation(v.look - v.pos, Vector3.up));
                            // aim=1：手电不和视线平行，而是照向视线正中看到的那一点（手拿着电筒指着看的地方）
                            beamGo.transform.localRotation = Quaternion.identity;
                            if (kv.TryGetValue("aim", out var am) && am == "1")
                            {
                                var fwd = camGo.transform.forward;
                                float reach = Physics.Raycast(v.pos, fwd, out var seen, 30f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) ? Mathf.Max(seen.distance, .5f) : 30f;
                                beamGo.transform.rotation = Quaternion.LookRotation(v.pos + fwd * reach - beamGo.transform.position, camGo.transform.up);
                            }
                            foreach (bool on in new[] { true, false })
                            {
                                if (!string.IsNullOrEmpty(states) && states != (on ? "on" : "off")) continue;
                                beam.enabled = on;
                                Random.InitState(6024); // 胶片颗粒每张一样，比较时颗粒不算差别
                                RenderJpg(cam, Path.Combine(outDir, $"{tag}_{v.name}_{(on ? "on" : "off")}.jpg"), 1600, 900);
                                count++;
                            }
                        }
                        // time=N：每个机位开着手电连画 N 帧，记平均每帧多少毫秒（粗略：编辑器批处理、每帧强制等显卡画完）
                        if (kv.TryGetValue("time", out var tm) && int.TryParse(tm, out int frames) && frames > 0)
                        {
                            beam.enabled = !ghost;
                            float sum = 0f; int n = 0;
                            var line = new System.Text.StringBuilder();
                            foreach (var v in views)
                            {
                                if (only != null && System.Array.IndexOf(only, v.name) < 0) continue;
                                camGo.transform.SetPositionAndRotation(v.pos, Quaternion.LookRotation(v.look - v.pos, Vector3.up));
                                float ms = TimeView(cam, frames);
                                sum += ms; n++;
                                line.Append(string.Format(CultureInfo.InvariantCulture, " {0}={1:F2}", v.name, ms));
                            }
                            if (n > 0) Debug.Log(string.Format(CultureInfo.InvariantCulture, "[Hako Art] 阴影计时 {0}：平均 {1:F2} ms/帧（{2} 帧 × {3} 个机位，1920×1080）{4}", tag, sum / n, frames, n, line));
                        }
                    }
                    finally
                    {
                        for (int i = undo.Count - 1; i >= 0; i--) undo[i]();
                    }
                }
            }
            finally
            {
                Object.DestroyImmediate(camGo);
                foreach (var ps in dust)
                    if (ps != null) ps.Clear(true);
            }
            Debug.Log($"[Hako Art] 阴影检查 {count} 张（{sets.Count} 组，{views.Count} 个机位，开 / 关手电）→ {outDir}");
        }

        /// <summary>
        /// face=1：开箱突脸的特写（环境光遮蔽会不会把定稿的脸弄脏）。照 OccupiedOpening.cs：箱女挂进座敷的箪笥、扑到终点，
        /// 手电已经被收走，只有脸前下方那盏冷白的补光（强度 3）；检视镜头的位置一张、拉到离两眼 0.2 m 的突脸终点一张。
        /// </summary>
        static int ShootGhostFace(Camera cam, string outDir, string tag)
        {
            var box = GameObject.Find("House_Blockout/Containers/Zashiki_Tansu_02");
            var cv = box != null ? box.GetComponentInChildren<ContainerVisual>(true) : null;
            if (cv == null) { Debug.LogWarning("[Hako Art] 阴影检查：找不到 Zashiki_Tansu_02，突脸特写没拍"); return 0; }
            HakoOnnaGhost ghost = null;
            GameObject lightGo = null;
            int shot = 0;
            try
            {
                cv.SetOpenImmediate(true);
                ghost = HakoOnnaGhost.Spawn(cv); // 每组新建一个：批处理里蒙皮只在实例第一次渲染时算
                ghost.Lunge = 1f;
                Transform le = null, re = null;
                foreach (var t in ghost.GetComponentsInChildren<Transform>(true))
                {
                    if (t.name == "Left_Eye") le = t;
                    else if (t.name == "Right_Eye") re = t;
                }
                if (le == null || re == null || ghost.Face == null) { Debug.LogWarning("[Hako Art] 阴影检查：箱女模型上找不到眼睛 / Face，突脸特写没拍"); return 0; }
                lightGo = new GameObject("ShadowCheckFaceLight");
                lightGo.transform.SetParent(ghost.Face, false);
                lightGo.transform.localPosition = new Vector3(0f, -.12f, .45f);
                lightGo.transform.localRotation = Quaternion.LookRotation(new Vector3(0f, .12f, -.45f));
                var fl = lightGo.AddComponent<Light>();
                fl.type = LightType.Spot;
                fl.spotAngle = 70f;
                fl.innerSpotAngle = 35f;
                fl.range = 2f;
                fl.color = new Color(.82f, .88f, 1f);
                fl.shadows = LightShadows.None;
                fl.intensity = 3f;
                // 检视镜头停的位置：和箱女检查图（HakoOnnaGhostSetup.Check）用同一个算法，那边是私有的，这里借来用
                var playerEye = typeof(HakoOnnaGhostSetup).GetMethod("PlayerEye", BindingFlags.NonPublic | BindingFlags.Static);
                if (playerEye == null) { Debug.LogWarning("[Hako Art] 阴影检查：HakoOnnaGhostSetup.PlayerEye 找不到了，突脸特写没拍"); return 0; }
                var (eyePos, eyeRot) = ((Vector3, Quaternion))playerEye.Invoke(null, new object[] { ContainerKind.Tansu });
                var camPos = box.transform.TransformPoint(eyePos / HakoOnnaGhost.FurnitureScale);
                var camRot = box.transform.rotation * eyeRot;
                var eyes = (le.position + re.position) * .5f;
                // 突脸终点：镜头沿着脸 → 检视镜头的连线拉到离两眼 0.2 m（美术设定 4.3）
                var pulled = eyes + (camPos - eyes).normalized * .2f;
                var pulledRot = Quaternion.FromToRotation(camRot * Vector3.forward, eyes - pulled) * camRot;
                foreach (var (name, pos, rot) in new[] { ("lunge", camPos, camRot), ("climax", pulled, pulledRot) })
                {
                    cam.transform.SetPositionAndRotation(pos, rot);
                    Random.InitState(6024);
                    RenderJpg(cam, Path.Combine(outDir, $"{tag}_ghostface_{name}.jpg"), 1600, 900);
                    shot++;
                }
            }
            finally
            {
                if (lightGo != null) Object.DestroyImmediate(lightGo);
                if (ghost != null) Object.DestroyImmediate(ghost.gameObject);
                cv.SetOpenImmediate(false);
            }
            return shot;
        }

        /// <summary>
        /// 第一人称手臂和手电筒的替身（PhaseOneSetup.CreatePlayerPrefab、NetworkPlayer.InitializeEquipment 的尺寸）：
        /// 只在试手电位置时打开，看它们会不会挡住手电的光、在画面里投出一大片影子。只投影、自己不画出来。
        /// </summary>
        static GameObject BuildArmProxies(Transform cam)
        {
            var root = new GameObject("ShadowCheckArms");
            root.transform.SetParent(cam, false);
            void Part(string name, PrimitiveType type, Transform parent, Vector3 pos, Vector3 euler, Vector3 scale)
            {
                var go = GameObject.CreatePrimitive(type);
                go.name = name;
                Object.DestroyImmediate(go.GetComponent<Collider>());
                go.transform.SetParent(parent, false);
                go.transform.localPosition = pos;
                go.transform.localRotation = Quaternion.Euler(euler);
                go.transform.localScale = scale;
                go.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.ShadowsOnly;
            }
            foreach (float side in new[] { -1f, 1f })
                Part(side < 0 ? "LeftArm" : "RightArm", PrimitiveType.Capsule, root.transform,
                    new Vector3(side * .27f, -.32f, .35f), new Vector3(70f, 0f, side * -10f), new Vector3(.1f, .23f, .1f));
            var lamp = new GameObject("Flashlight").transform;
            lamp.SetParent(root.transform, false);
            Part("Barrel", PrimitiveType.Cylinder, lamp, Vector3.zero, new Vector3(90f, 0f, 0f), new Vector3(.047f, .075f, .047f));
            Part("Lens", PrimitiveType.Cylinder, lamp, Vector3.forward * .08f, new Vector3(90f, 0f, 0f), new Vector3(.065f, .018f, .065f));
            root.SetActive(false);
            return root;
        }

        static string ShadowArg(string name)
        {
            var args = System.Environment.GetCommandLineArgs();
            int i = System.Array.IndexOf(args, name);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : "";
        }

        static float F(string s) => float.Parse(s, CultureInfo.InvariantCulture);

        static Vector3 V3(string s)
        {
            var p = s.Split(':');
            return new Vector3(F(p[0]), F(p[1]), F(p[2]));
        }

        /// <summary>临时改参数（只改内存里的对象，拍完用 undo 改回去；不标脏、不存盘）。</summary>
        static void ApplyShadowOverrides(Dictionary<string, string> kv, List<System.Action> undo)
        {
            var urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            object ssao = null;
            var rd = AssetDatabase.LoadAssetAtPath<ScriptableRendererData>(ShadowRendererPath);
            if (rd != null)
                foreach (var f in rd.rendererFeatures)
                    if (f != null && f.GetType().Name == "ScreenSpaceAmbientOcclusion")
                        ssao = f.GetType().GetField("m_Settings", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(f);

            var ssaoKeys = new Dictionary<string, string>
            {
                { "ssaoI", "Intensity" }, { "ssaoR", "Radius" }, { "ssaoD", "DirectLightingStrength" }, { "ssaoS", "Samples" },
                { "ssaoF", "Falloff" }, { "ssaoB", "BlurQuality" }, { "ssaoM", "AOMethod" }, { "ssaoN", "NormalSamples" }, { "ssaoDown", "Downsample" },
            };
            var urpKeys = new Dictionary<string, string>
            {
                { "dist", "m_ShadowDistance" }, { "res", "m_MainLightShadowmapResolution" }, { "ares", "m_AdditionalLightsShadowmapResolution" },
                { "casc", "m_ShadowCascadeCount" }, { "bias", "m_ShadowDepthBias" }, { "nbias", "m_ShadowNormalBias" },
                { "tierHigh", "m_AdditionalLightsShadowResolutionTierHigh" }, { "tierMed", "m_AdditionalLightsShadowResolutionTierMedium" },
                { "soft", "m_SoftShadowQuality" }, { "opaque", "m_RequireOpaqueTexture" }, { "split2", "m_Cascade2Split" }, { "split3", "m_Cascade3Split" },
            };
            foreach (var p in kv)
            {
                if (ssaoKeys.TryGetValue(p.Key, out var sf)) { if (ssao != null) SetFieldTemp(ssao, sf, p.Value, undo); }
                else if (urpKeys.TryGetValue(p.Key, out var uf)) { if (urp != null) SetFieldTemp(urp, uf, p.Value, undo); }
            }

            // expo：常态后处理的曝光（档）。游戏里的画面很暗，暗处的影子在截图里分不清，调高几档只是为了看清形状
            if (kv.TryGetValue("expo", out var ex))
            {
                var vp = AssetDatabase.LoadAssetAtPath<VolumeProfile>("Assets/_Art/Settings/VP_Normal.asset");
                if (vp != null && vp.TryGet(out ColorAdjustments ca))
                {
                    float oldExposure = ca.postExposure.value;
                    ca.postExposure.value = F(ex);
                    undo.Add(() => ca.postExposure.value = oldExposure);
                }
            }

            var moonGo = GameObject.Find("Moonlight");
            if (moonGo != null && kv.TryGetValue("moonStr", out var ms))
            {
                var moon = moonGo.GetComponent<Light>();
                float old = moon.shadowStrength;
                moon.shadowStrength = F(ms);
                undo.Add(() => moon.shadowStrength = old);
            }

            var landmarks = GameObject.Find("House_Blockout/Lighting/Landmarks");
            if (landmarks != null)
                foreach (var l in landmarks.GetComponentsInChildren<Light>())
                {
                    var light = l;
                    var oldShadows = light.shadows;
                    float oldStr = light.shadowStrength, oldNear = light.shadowNearPlane;
                    if (kv.TryGetValue("lm", out var lm)) light.shadows = lm == "1" ? LightShadows.Soft : lm == "2" ? LightShadows.Hard : LightShadows.None;
                    if (kv.TryGetValue("lmStr", out var ls)) light.shadowStrength = F(ls);
                    if (kv.TryGetValue("lmNear", out var ln)) light.shadowNearPlane = F(ln);
                    UniversalAdditionalLightData added = null;
                    if (kv.TryGetValue("lmTier", out var lt))
                    {
                        if (!light.TryGetComponent<UniversalAdditionalLightData>(out var data)) data = added = light.gameObject.AddComponent<UniversalAdditionalLightData>();
                        SetFieldTemp(data, "m_AdditionalLightsShadowResolutionTier", lt, undo); // 属性只读：0 低、1 中、2 高（URP 资产里各档的分辨率）
                    }
                    undo.Add(() =>
                    {
                        light.shadows = oldShadows;
                        light.shadowStrength = oldStr;
                        light.shadowNearPlane = oldNear;
                        if (added != null) Object.DestroyImmediate(added);
                    });
                }
        }

        static void SetFieldTemp(object target, string field, string value, List<System.Action> undo)
        {
            var f = target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (f == null) { Debug.LogWarning($"[Hako Art] 阴影检查：{target.GetType().Name} 没有字段 {field}"); return; }
            object old = f.GetValue(target);
            object v;
            if (f.FieldType.IsEnum) v = System.Enum.ToObject(f.FieldType, int.Parse(value, CultureInfo.InvariantCulture));
            else if (f.FieldType == typeof(bool)) v = value == "1" || value == "true";
            else if (f.FieldType == typeof(Vector2)) { var p = value.Split(':'); v = new Vector2(F(p[0]), F(p[1])); }
            else v = System.Convert.ChangeType(value, f.FieldType, CultureInfo.InvariantCulture);
            f.SetValue(target, v);
            undo.Add(() => f.SetValue(target, old));
        }

        /// <summary>同一个画面连画 frames 帧的平均毫秒数。每帧读回 1 个像素，逼显卡把这一帧画完再计时。</summary>
        static float TimeView(Camera cam, int frames)
        {
            var rt = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            rt.Create();
            cam.targetTexture = rt;
            var one = new Texture2D(1, 1, TextureFormat.RGB24, false);
            var req = new RenderPipeline.StandardRequest { destination = rt };
            bool viaRequest = RenderPipeline.SupportsRenderRequest(cam, req);
            var prev = RenderTexture.active;
            void Frame()
            {
                if (viaRequest) RenderPipeline.SubmitRenderRequest(cam, req);
                else cam.Render();
                RenderTexture.active = rt;
                one.ReadPixels(new Rect(0, 0, 1, 1), 0, 0);
            }
            for (int i = 0; i < 4; i++) Frame(); // 预热：着色器变体、阴影图集
            var sw = System.Diagnostics.Stopwatch.StartNew();
            for (int i = 0; i < frames; i++) Frame();
            sw.Stop();
            RenderTexture.active = prev;
            cam.targetTexture = null;
            Object.DestroyImmediate(one);
            rt.Release();
            Object.DestroyImmediate(rt);
            return (float)sw.Elapsed.TotalMilliseconds / frames;
        }

        static void RenderJpg(Camera cam, string path, int w, int h)
        {
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
            try { File.WriteAllBytes(path, tex.EncodeToJPG(92)); }
            catch (IOException e) { Debug.LogWarning($"[Hako Art] 截图 {path} 写不进去（文件被占用？），跳过：{e.Message}"); }
            cam.targetTexture = null;
            Object.DestroyImmediate(tex);
            rt.Release();
            Object.DestroyImmediate(rt);
        }
    }
}
