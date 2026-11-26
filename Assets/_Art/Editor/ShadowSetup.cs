using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace HakoOnna.Art.EditorTools
{
    /// <summary>
    /// 阴影（2026-11-26，用户要求“给游戏场景补阴影”，看过对比图后定的“补阴影后”那一组）。
    /// 检查下来阴影功能本来就开着（月光、手电都是软阴影），画面上看不到影子有三个原因：
    /// <list type="number">
    /// <item>手电离眼睛只有 25 cm（NetworkPlayer.Inspection.cs），影子几乎全被物体自己挡住——程序那边的数，这里不改。</item>
    /// <item>屋里其余的光是没有方向的环境光，照不出影子；环境光遮蔽原来是 URP 模板的默认值（强度 0.4），几乎看不出来。
    ///   这里调强，墙角、家具脚下、缝里会暗下去。</item>
    /// <item>六盏房间微光原来不投影，光穿过桌子照到桌底的地面，桌子像浮着（Landmarks.cs 里已改成投影）。</item>
    /// </list>
    /// 另外月光的阴影图加大、阴影距离缩短，縁側地板上才看得清玻璃门的格子影。
    /// 这些数写在 URP 的两个资产里（PC 画质档）：每次生成都检查一遍，和下面不一样就写回去——要改就改这里的数。
    /// 试参数、出对比图用 ArtSnapshot.Shadows.cs（Hako → Art → 9f）。
    /// </summary>
    internal static class ShadowSetup
    {
        const string PipelinePath = "Assets/Settings/PC_RPAsset.asset";
        const string RendererPath = "Assets/Settings/PC_Renderer.asset";

        // ---- 环境光遮蔽（Screen Space Ambient Occlusion） ----
        /// <summary>强度。模板默认 0.4。</summary>
        internal const float AoIntensity = 2.5f;
        /// <summary>半径（米）。0.5 以上会在物体周围糊出一圈黑，院子的草也会发暗。</summary>
        internal const float AoRadius = .3f;
        /// <summary>
        /// 直射光（手电、月光、房间微光、箱女脸上的补光）下的强度，0–1。保持模板的 0.25，不要调高：
        /// 0.5 时开箱突脸的和服和脸开始发脏，1.0 时满是黑斑和噪点（镜头离她 0.2–0.5 m，0.3 m 的采样半径比画面还大）。
        /// </summary>
        internal const float AoDirect = .25f;
        /// <summary>采样档：0 = 高（12 次）、1 = 中（8 次）、2 = 低（4 次）。中档 + 中档模糊会在物体周围留下一块块的噪点。</summary>
        internal const int AoSamples = 0;

        // ---- 阴影图 ----
        /// <summary>阴影距离（米）。原来 50；指数平方雾 0.17 在 12 m 处只剩 1.6%，再远的影子看不见。</summary>
        internal const float ShadowDistance = 20f;
        /// <summary>月光阴影的级联数。原来 4；20 m 分 3 级（2 / 6 / 20 m），雾里看和 30 m 分 4 级一样。</summary>
        internal const int Cascades = 3;
        internal static readonly Vector2 CascadeSplit = new Vector2(.1f, .3f);
        /// <summary>月光阴影图。原来 2048；用 2048 时縁側的格子影明显更糊。</summary>
        internal const int MainShadowResolution = 4096;
        /// <summary>
        /// 手电、房间微光共用的阴影图集的上限。原来 2048：手电 1 张 1024 + 六盏微光放不下时 URP 会把所有阴影图减半。
        /// 这只是上限，URP 按实际用到的大小分配。
        /// </summary>
        internal const int AdditionalShadowAtlas = 4096;

        public static void Apply()
        {
            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);
            if (pipeline == null) Debug.LogWarning($"[Hako Art] 阴影：找不到 {PipelinePath}，阴影图的设置没写");
            else
            {
                var so = new SerializedObject(pipeline);
                bool changed = SetFloat(so, "m_ShadowDistance", ShadowDistance);
                changed |= SetInt(so, "m_ShadowCascadeCount", Cascades);
                changed |= SetVector2(so, "m_Cascade3Split", CascadeSplit);
                changed |= SetInt(so, "m_MainLightShadowmapResolution", MainShadowResolution);
                changed |= SetInt(so, "m_AdditionalLightsShadowmapResolution", AdditionalShadowAtlas);
                Save(so, pipeline, changed, "阴影图（PC_RPAsset）");
            }

            var renderer = AssetDatabase.LoadAssetAtPath<ScriptableRendererData>(RendererPath);
            ScriptableRendererFeature ssao = null;
            if (renderer != null)
                foreach (var f in renderer.rendererFeatures)
                    if (f != null && f.GetType().Name == "ScreenSpaceAmbientOcclusion") ssao = f;
            if (ssao == null) Debug.LogWarning($"[Hako Art] 阴影：{RendererPath} 里没有 Screen Space Ambient Occlusion，环境光遮蔽的设置没写");
            else
            {
                var so = new SerializedObject(ssao);
                bool changed = SetFloat(so, "m_Settings.Intensity", AoIntensity);
                changed |= SetFloat(so, "m_Settings.Radius", AoRadius);
                changed |= SetFloat(so, "m_Settings.DirectLightingStrength", AoDirect);
                changed |= SetInt(so, "m_Settings.Samples", AoSamples);
                var active = so.FindProperty("m_Active");
                if (active != null && !active.boolValue) { active.boolValue = true; changed = true; }
                Save(so, ssao, changed, "环境光遮蔽（PC_Renderer）");
            }
        }

        static bool SetFloat(SerializedObject so, string path, float value)
        {
            var p = so.FindProperty(path);
            if (p == null) { Debug.LogWarning($"[Hako Art] 阴影：{so.targetObject.name} 没有 {path}"); return false; }
            if (Mathf.Approximately(p.floatValue, value)) return false;
            p.floatValue = value;
            return true;
        }

        static bool SetInt(SerializedObject so, string path, int value)
        {
            var p = so.FindProperty(path);
            if (p == null) { Debug.LogWarning($"[Hako Art] 阴影：{so.targetObject.name} 没有 {path}"); return false; }
            if (p.intValue == value) return false;
            p.intValue = value;
            return true;
        }

        static bool SetVector2(SerializedObject so, string path, Vector2 value)
        {
            var p = so.FindProperty(path);
            if (p == null) { Debug.LogWarning($"[Hako Art] 阴影：{so.targetObject.name} 没有 {path}"); return false; }
            if ((p.vector2Value - value).sqrMagnitude < 1e-8f) return false;
            p.vector2Value = value;
            return true;
        }

        static void Save(SerializedObject so, Object asset, bool changed, string what)
        {
            if (!changed) return;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssetIfDirty(asset);
            Debug.Log($"[Hako Art] 阴影：{what} 已更新");
        }
    }
}
