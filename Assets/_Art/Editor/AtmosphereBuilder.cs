using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace HakoOnna.Art.EditorTools
{
    /// <summary>
    /// 月光、环境光、雾、常态与猎杀两套后处理 Volume。
    /// 第二轮（2026-11-19）整体压暗：没有手电时只看得清月光照到的地方。
    /// 雾用指数平方（用户定的，美术圣经已改）：密度 0.17，2.5 m 内基本清楚、5 m 一半、7.5 m 以外看不清。
    /// </summary>
    internal static class AtmosphereBuilder
    {
        const string ProfileDir = "Assets/_Art/Settings";
        const string NormalPath = ProfileDir + "/VP_Normal.asset";
        const string HuntPath = ProfileDir + "/VP_Hunt.asset";

        public static void Build(Transform parent)
        {
            // 月光：从南侧斜射，穿过縁側的开口照进房间
            var moonGo = new GameObject("Moonlight");
            moonGo.transform.SetParent(parent, false);
            moonGo.transform.rotation = Quaternion.Euler(28f, -35f, 0f);
            var moon = moonGo.AddComponent<Light>();
            moon.type = LightType.Directional;
            moon.color = new Color(0.50f, 0.60f, 0.92f);
            moon.intensity = 0.35f;
            moon.shadows = LightShadows.Soft;
            RenderSettings.sun = moon;

            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.07f, 0.078f, 0.105f); // 2026-11-23 用户实机后两次要求调亮，“至少能看清自己的房间”：(.011, .013, .02) → (.022, .026, .04) → 现在
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(0.012f, 0.015f, 0.022f);
            RenderSettings.fogDensity = 0.17f; // 0.085 时游戏里还是看得太清楚，用户要求距离缩短一半

            ArtKit.EnsureFolder(ProfileDir);
            var normal = LoadOrCreate(NormalPath, FillNormal);
            var hunt = LoadOrCreate(HuntPath, FillHunt);
            UpgradeNormalR2(normal);
            EnsureBloomGuard(normal);
            // 2026-11-23：整体曝光 0.1 → 0.3（同上）。每次生成都写，要改就改这个数
            if (normal.TryGet(out ColorAdjustments exposure) && !Mathf.Approximately(exposure.postExposure.value, NormalExposure))
            {
                exposure.postExposure.Override(NormalExposure);
                EditorUtility.SetDirty(exposure);
            }

            var globalGo = new GameObject("PostFX_Normal");
            globalGo.transform.SetParent(parent, false);
            var gv = globalGo.AddComponent<Volume>();
            gv.isGlobal = true;
            gv.priority = 0f;
            gv.sharedProfile = normal;

            var huntGo = new GameObject("PostFX_Hunt");
            huntGo.transform.SetParent(parent, false);
            var hv = huntGo.AddComponent<Volume>();
            hv.isGlobal = true;
            hv.priority = 10f;
            hv.weight = 0f;
            hv.sharedProfile = hunt;
            huntGo.AddComponent<HuntAtmosphere>().Bind(hv);

            // 预览相机：程序接入玩家相机后删除
            var camGo = new GameObject("BlockoutPreviewCamera_DeleteLater");
            camGo.transform.SetParent(parent, false);
            camGo.transform.SetPositionAndRotation(HouseBlockoutGenerator.MapOldPoint(new Vector3(2.7f, 1.6f, 1f)), Quaternion.Euler(5f, 10f, 0f));
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.01f, 0.012f, 0.02f);
            cam.nearClipPlane = 0.05f;
            cam.fieldOfView = 75f;
            cam.GetUniversalAdditionalCameraData().renderPostProcessing = true;
        }

        static VolumeProfile LoadOrCreate(string path, System.Action<VolumeProfile> fill)
        {
            var existing = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if (existing != null) return existing; // 不覆盖手动调整过的参数

            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, path);
            fill(profile);
            foreach (var c in profile.components)
            {
                c.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;
                AssetDatabase.AddObjectToAsset(c, profile);
            }
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            return profile;
        }

        /// <summary>参与泛光计算的亮度上限（HDR）。灯火、月光照亮的墙面照样有柔光，单个高光点扩不成大光斑。</summary>
        internal const float BloomClamp = 4f;

        /// <summary>
        /// URP 资产（PC_RPAsset）自带的 SampleSceneProfile 开着 Bloom（强度 0.25、亮度不封顶）。
        /// 月光打在镜子、玻璃、水面这类很光滑的材质上，高光点能亮到上千，被泛光放大成一大团白光
        /// （上二楼的楼梯处最明显）。这里在常态 Volume 里接管 Bloom：强度、阈值、扩散保持原样，只加亮度上限。
        /// 已有的设置不覆盖（和 LoadOrCreate 一样尊重手调过的参数）。
        /// </summary>
        static void EnsureBloomGuard(VolumeProfile p)
        {
            bool changed = false;
            if (!p.TryGet(out Bloom bloom))
            {
                bloom = p.Add<Bloom>();
                bloom.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;
                AssetDatabase.AddObjectToAsset(bloom, p);
                changed = true;
            }
            if (!bloom.intensity.overrideState) { bloom.intensity.Override(0.25f); changed = true; }
            if (!bloom.threshold.overrideState) { bloom.threshold.Override(1f); changed = true; }
            if (!bloom.scatter.overrideState) { bloom.scatter.Override(0.5f); changed = true; }
            if (!bloom.clamp.overrideState) { bloom.clamp.Override(BloomClamp); changed = true; }
            if (!changed) return;
            EditorUtility.SetDirty(bloom);
            EditorUtility.SetDirty(p);
            AssetDatabase.SaveAssets();
        }

        const float NormalExposure = 0.6f;

        static void FillNormal(VolumeProfile p)
        {
            p.Add<Tonemapping>().mode.Override(TonemappingMode.ACES);

            var ca = p.Add<ColorAdjustments>();
            ca.postExposure.Override(0.1f);
            ca.contrast.Override(14f);
            ca.saturation.Override(-28f);

            p.Add<WhiteBalance>().temperature.Override(-12f);

            var vg = p.Add<Vignette>();
            vg.intensity.Override(0.4f);
            vg.smoothness.Override(0.5f);

            var fg = p.Add<FilmGrain>();
            fg.type.Override(FilmGrainLookup.Medium3);
            fg.intensity.Override(0.38f);

            p.Add<ShadowsMidtonesHighlights>().shadows.Override(ShadowTint);
        }

        /// <summary>阴影偏冷的蓝灰（美术圣经 §2）：xyz 为颜色倍数，w 为亮度偏移。</summary>
        static readonly Vector4 ShadowTint = new Vector4(0.92f, 0.97f, 1.10f, 0f);

        /// <summary>
        /// 第二轮：已有的 VP_Normal 升级一次（压暗、降饱和、颗粒更粗、阴影偏冷）。
        /// 以“还没有 ShadowsMidtonesHighlights”为准，升级过以后手调的参数不再覆盖。
        /// </summary>
        static void UpgradeNormalR2(VolumeProfile p)
        {
            if (p.Has<ShadowsMidtonesHighlights>()) return;
            if (p.TryGet(out ColorAdjustments ca))
            {
                ca.postExposure.Override(0.1f);
                ca.contrast.Override(14f);
                ca.saturation.Override(-28f);
                EditorUtility.SetDirty(ca);
            }
            if (p.TryGet(out WhiteBalance wb)) { wb.temperature.Override(-12f); EditorUtility.SetDirty(wb); }
            if (p.TryGet(out Vignette vg))
            {
                vg.intensity.Override(0.4f);
                vg.smoothness.Override(0.5f);
                EditorUtility.SetDirty(vg);
            }
            if (p.TryGet(out FilmGrain fg))
            {
                fg.type.Override(FilmGrainLookup.Medium3);
                fg.intensity.Override(0.38f);
                EditorUtility.SetDirty(fg);
            }
            var smh = p.Add<ShadowsMidtonesHighlights>();
            smh.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;
            smh.shadows.Override(ShadowTint);
            AssetDatabase.AddObjectToAsset(smh, p);
            EditorUtility.SetDirty(smh);
            EditorUtility.SetDirty(p);
            AssetDatabase.SaveAssets();
            Debug.Log("[Hako Art] VP_Normal 升级到第二轮（更暗、更冷）");
        }

        static void FillHunt(VolumeProfile p)
        {
            var ca = p.Add<ColorAdjustments>();
            ca.saturation.Override(-55f);
            ca.contrast.Override(20f);
            ca.colorFilter.Override(new Color(1f, 0.82f, 0.80f));

            var vg = p.Add<Vignette>();
            vg.intensity.Override(0.5f);
            vg.color.Override(new Color(0.25f, 0f, 0f));

            p.Add<ChromaticAberration>().intensity.Override(0.35f);
            p.Add<LensDistortion>().intensity.Override(-0.18f);
        }
    }
}
