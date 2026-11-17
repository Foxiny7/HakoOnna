using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace HakoOnna.Art.EditorTools
{
    /// <summary>灰盒搭建用的通用工具。</summary>
    internal static class ArtKit
    {
        public static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int slash = path.LastIndexOf('/');
            string parent = path.Substring(0, slash);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, path.Substring(slash + 1));
        }

        /// <summary>创建一个立方体，localCenter 为中心点，size 为尺寸（米）。</summary>
        public static GameObject Box(Transform parent, string name, Vector3 localCenter, Vector3 size,
            Material mat, bool collider = true, bool castShadows = true)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localCenter;
            go.transform.localScale = size;
            var mr = go.GetComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            if (!castShadows) mr.shadowCastingMode = ShadowCastingMode.Off;
            if (!collider) Object.DestroyImmediate(go.GetComponent<BoxCollider>());
            return go;
        }

        public static GameObject Cylinder(Transform parent, string name, Vector3 localCenter, Vector3 scale,
            Quaternion localRot, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localCenter;
            go.transform.localRotation = localRot;
            go.transform.localScale = scale;
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }

        public static Transform Empty(Transform parent, string name, Vector3 localPos)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.localPosition = localPos;
            return t;
        }

        public static void MarkStaticRecursive(GameObject go)
        {
            const StaticEditorFlags flags = StaticEditorFlags.ContributeGI | StaticEditorFlags.OccluderStatic |
                                            StaticEditorFlags.OccludeeStatic | StaticEditorFlags.BatchingStatic |
                                            StaticEditorFlags.ReflectionProbeStatic;
            foreach (var t in go.GetComponentsInChildren<Transform>(true))
                GameObjectUtility.SetStaticEditorFlags(t.gameObject, flags);
        }
    }

    /// <summary>
    /// 灰盒占位材质。只在材质不存在时创建并设置颜色；
    /// 之后你在 Unity 里替换贴图或改颜色，重新生成场景不会覆盖。
    /// </summary>
    internal sealed class Palette
    {
        const string Dir = "Assets/_Art/Materials/Placeholder";

        public Material Plaster, WoodFloor, Ceiling, Roof, Tatami, Stone, Tile, Ground, Fence;
        public Material WoodDarkA, WoodDarkB, WoodMidA, WoodMidB, WoodLightA, WoodLightB;
        public Material WickerA, WickerB, FusumaA, FusumaB, Paper, Interior;
        public Material Metal, Rust, Carpet, Lacquer;

        public static Palette Load()
        {
            ArtKit.EnsureFolder(Dir);
            var p = new Palette
            {
                Plaster = M("M_Plaster_Tsuchikabe", 0.72f, 0.66f, 0.55f, 0.1f),
                WoodFloor = M("M_Wood_Floor", 0.32f, 0.22f, 0.14f, 0.35f),
                Ceiling = M("M_Wood_Ceiling", 0.30f, 0.22f, 0.15f, 0.15f),
                Roof = M("M_Roof", 0.10f, 0.10f, 0.11f, 0.1f),
                Tatami = M("M_Tatami", 0.52f, 0.50f, 0.33f, 0.1f),
                Stone = M("M_Stone_Doma", 0.35f, 0.34f, 0.32f, 0.15f),
                Tile = M("M_Tile_Bath", 0.55f, 0.60f, 0.60f, 0.5f),
                Ground = M("M_Ground_Garden", 0.16f, 0.15f, 0.12f, 0.05f),
                Fence = M("M_Fence", 0.30f, 0.27f, 0.24f, 0.1f),
                WoodDarkA = M("M_Wood_Dark_A", 0.22f, 0.14f, 0.09f, 0.3f),
                WoodDarkB = M("M_Wood_Dark_B", 0.27f, 0.18f, 0.11f, 0.25f),
                WoodMidA = M("M_Wood_Mid_A", 0.42f, 0.30f, 0.19f, 0.3f),
                WoodMidB = M("M_Wood_Mid_B", 0.38f, 0.28f, 0.20f, 0.25f),
                WoodLightA = M("M_Wood_Kiri_A", 0.62f, 0.50f, 0.36f, 0.2f),
                WoodLightB = M("M_Wood_Kiri_B", 0.56f, 0.46f, 0.34f, 0.2f),
                WickerA = M("M_Wicker_A", 0.55f, 0.42f, 0.25f, 0.1f),
                WickerB = M("M_Wicker_B", 0.48f, 0.38f, 0.24f, 0.1f),
                FusumaA = M("M_Fusuma_A", 0.70f, 0.66f, 0.55f, 0.1f),
                FusumaB = M("M_Fusuma_B", 0.60f, 0.60f, 0.52f, 0.1f),
                Paper = M("M_Shoji_Paper", 0.85f, 0.80f, 0.68f, 0.05f),
                Interior = M("M_Container_Interior", 0.015f, 0.012f, 0.010f, 0f),
                Metal = M("M_Metal_Dark", 0.12f, 0.14f, 0.12f, 0.45f, 0.7f),
                Rust = M("M_Metal_Rust", 0.35f, 0.20f, 0.12f, 0.2f, 0.5f),
                Carpet = M("M_Carpet_Old", 0.36f, 0.12f, 0.10f, 0.05f),
                Lacquer = M("M_Lacquer_Black", 0.08f, 0.03f, 0.03f, 0.6f),
            };
            return p;
        }

        static Material M(string name, float r, float g, float b, float smoothness, float metallic = 0f)
        {
            string path = $"{Dir}/{name}.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;

            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            var m = new Material(shader) { name = name };
            var c = new Color(r, g, b, 1f);
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Color")) m.SetColor("_Color", c);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smoothness);
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", smoothness);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metallic);
            AssetDatabase.CreateAsset(m, path);
            return m;
        }
    }
}
