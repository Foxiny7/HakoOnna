using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace HakoOnna.Art.EditorTools
{
    /// <summary>
    /// 第六轮（2026-11-24，院外风景）：夜空天空盒（Shaders/HakoNightSky.shader，材质 Materials/Sky/MX_NightSky）。
    /// <list type="bullet">
    /// <item>云用 Poly Haven kloppenheim_07_puresky（CC0，4K .hdr，阴天、云缝里有月亮）。</item>
    /// <item>每次生成都按 Moonlight 的方向摆月亮，并把 HDRI 转到它的月亮和我们的月亮同一个方位
    ///   （它的月亮高 10°，在 shader 里用旁边的云盖掉；我们的月亮正好落在它下面那块云缝里）。</item>
    /// <item>远山画在天空盒里（不是模型），开雾时是一层层比雾色略亮的剪影，GhostSight 关雾时按环境光打亮。</item>
    /// <item>材质参数只在版本标签（HakoArtRev）不是 <see cref="Rev"/> 时重置成 shader 默认值；在 Unity 里手调过的保留。</item>
    /// <item>天空不参与环境光（场景用 Flat 环境光）；反射见 <see cref="Apply"/>。</item>
    /// </list>
    /// </summary>
    internal static class SkyBuilder
    {
        const string ShaderName = "HakoOnna/NightSky";
        const string MatDir = "Assets/_Art/Materials/Sky";
        const string MatPath = MatDir + "/MX_NightSky.mat";
        internal const string PanoPath = ExternalMats.Root + "/PolyHaven/kloppenheim_07_puresky/kloppenheim_07_puresky_4k.hdr";
        const string Rev = "r6";

        /// <summary>HDRI 里月亮的位置（Tools~/hdri_moon_stats.py 量的最亮点）：u，v 从图片顶边往下量。</summary>
        const float PanoMoonU = .59924f, PanoMoonV = .28784f;

        public static Material Apply(Light moon)
        {
            var shader = Shader.Find(ShaderName);
            if (shader == null)
            {
                Debug.LogWarning($"[Hako Art] 找不到 {ShaderName}，院外没有天空（相机按背景色清屏）");
                RenderSettings.skybox = null;
                return null;
            }
            var pano = AssetDatabase.LoadAssetAtPath<Texture2D>(PanoPath);
            if (pano == null) Debug.LogWarning($"[Hako Art] 缺少天空贴图 {PanoPath}（只有月亮和远山）");

            ArtKit.EnsureFolder(MatDir);
            var m = AssetDatabase.LoadAssetAtPath<Material>(MatPath);
            bool created = m == null;
            if (created) m = new Material(shader) { name = "MX_NightSky" };
            if (m.shader != shader) m.shader = shader;
            if (!created && m.GetTag("HakoArtRev", false, "") != Rev)
            {
                var fresh = new Material(shader);
                m.CopyPropertiesFromMaterial(fresh);
                Object.DestroyImmediate(fresh);
            }
            m.SetOverrideTag("HakoArtRev", Rev);
            m.SetTexture("_Pano", pano);

            // 月亮在 Moonlight 照过来的反方向
            var toMoon = moon != null ? -moon.transform.forward : new Vector3(.5064f, .4695f, -.7233f);
            toMoon.Normalize();
            float az = Mathf.Atan2(toMoon.x, toMoon.z) * Mathf.Rad2Deg;
            float offset = Mathf.Repeat(PanoMoonU - az / 360f, 1f);
            float el = (90f - PanoMoonV * 180f) * Mathf.Deg2Rad;
            var panoMoon = new Vector3(Mathf.Sin(az * Mathf.Deg2Rad) * Mathf.Cos(el), Mathf.Sin(el), Mathf.Cos(az * Mathf.Deg2Rad) * Mathf.Cos(el));
            m.SetVector("_MoonDir", toMoon);
            m.SetVector("_PanoMoonDir", panoMoon);
            m.SetFloat("_PanoOffset", offset);
            if (created) AssetDatabase.CreateAsset(m, MatPath);
            else EditorUtility.SetDirty(m);

            var before = ReflectionProbe.defaultTexture;
            RenderSettings.skybox = m;
            Debug.Log($"[Hako Art] 夜空：月亮方位 {Mathf.Repeat(az, 360f):F1}°、仰角 {Mathf.Asin(toMoon.y) * Mathf.Rad2Deg:F1}°；HDRI 偏移 {offset:F4}；" +
                      $"默认反射 {Describe(before)} → {Describe(ReflectionProbe.defaultTexture)}，反射模式 {RenderSettings.defaultReflectionMode}");
            return m;
        }

        static string Describe(Texture t) => t == null ? "无" : $"{t.name}（{t.dimension} {t.width}px）";
    }
}
