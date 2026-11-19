using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace HakoOnna.Art.EditorTools
{
    /// <summary>月光、环境光、雾、常态与猎杀两套后处理 Volume。</summary>
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
            moon.color = new Color(0.55f, 0.64f, 0.90f);
            moon.intensity = 0.35f;
            moon.shadows = LightShadows.Soft;
            RenderSettings.sun = moon;

            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.025f, 0.03f, 0.045f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.02f, 0.025f, 0.035f);
            RenderSettings.fogStartDistance = 4f;
            RenderSettings.fogEndDistance = 30f;

            ArtKit.EnsureFolder(ProfileDir);
            var normal = LoadOrCreate(NormalPath, FillNormal);
            var hunt = LoadOrCreate(HuntPath, FillHunt);
            EnsureBloomGuard(normal);

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

        static void FillNormal(VolumeProfile p)
        {
            p.Add<Tonemapping>().mode.Override(TonemappingMode.ACES);

            var ca = p.Add<ColorAdjustments>();
            ca.postExposure.Override(0.3f);
            ca.contrast.Override(10f);
            ca.saturation.Override(-20f);

            p.Add<WhiteBalance>().temperature.Override(-8f);

            var vg = p.Add<Vignette>();
            vg.intensity.Override(0.32f);
            vg.smoothness.Override(0.45f);

            var fg = p.Add<FilmGrain>();
            fg.type.Override(FilmGrainLookup.Medium2);
            fg.intensity.Override(0.3f);
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
