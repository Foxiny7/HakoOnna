using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace HakoOnna.Art.EditorTools
{
    /// <summary>
    /// 美术检查用截图：从固定机位把当前老宅场景渲染成 PNG（Docs/screenshots/detail）。
    /// 白天 / 室内补光只在截图时临时改，结束后恢复，不会存进场景。
    /// 命令行：Unity -batchmode -executeMethod HakoOnna.Art.EditorTools.ArtSnapshot.GenerateAndShoot -quit
    /// </summary>
    public static class ArtSnapshot
    {
        const string OutDir = "Docs/screenshots/detail";

        /// <summary>Detail：室内灯光，机位直接用新坐标书写（不搬家映射，也不参与“机位前方不放家具”）。</summary>
        enum Mode { Day, Interior, Mood, Plan1F, Plan2F, Detail }

        /// <summary>室外机位：平面和高度一起放大，构图和旧版一致。</summary>
        static float ExteriorScale => HouseBlockoutGenerator.PlanStretch;

        struct Shot
        {
            public string name;
            public Vector3 pos, look;
            public float fov;
            public Mode mode;
            public Shot(string n, Mode m, Vector3 p, Vector3 l, float f = 70f) { name = n; mode = m; pos = p; look = l; fov = f; }
        }

        public static void GenerateAndShoot()
        {
            HouseBlockoutGenerator.Generate();
            Shoot();
        }

        /// <summary>室内机位和注视点（新坐标）。按房间补家具时机位前方留空，截图不会被贴脸的家具挡住。</summary>
        internal static IEnumerable<(Vector3 pos, Vector3 look)> InteriorCameraSpots()
        {
            foreach (var s in Shots())
                if (s.mode == Mode.Interior || s.mode == Mode.Mood)
                    yield return (HouseBlockoutGenerator.MapOldPoint(s.pos), HouseBlockoutGenerator.MapOldPoint(s.look));
        }

        static IEnumerable<Shot> Shots()
        {
            yield return new Shot("plan_1F", Mode.Plan1F, new Vector3(13.2f, 2.5f, 14.5f), Vector3.zero, 17f);
            yield return new Shot("plan_2F", Mode.Plan2F, new Vector3(12.9f, 5.3f, 13.8f), Vector3.zero, 8.6f);
            yield return new Shot("ext_southwest", Mode.Day, new Vector3(-9f, 10f, -15f), new Vector3(13f, 2f, 9f), 50f);
            yield return new Shot("ext_northeast", Mode.Day, new Vector3(36f, 10f, 34f), new Vector3(13f, 3f, 15f), 50f);
            yield return new Shot("ext_garden_engawa", Mode.Day, new Vector3(17f, 1.6f, -6.2f), new Vector3(16f, 1.8f, 1f), 65f);
            yield return new Shot("ext_gate", Mode.Day, new Vector3(3.86f, 1.65f, -11.4f), new Vector3(3.86f, 2.3f, -6f), 60f);
            yield return new Shot("ext_back_kura", Mode.Day, new Vector3(-3.5f, 2.2f, 32f), new Vector3(8f, 2f, 21f), 60f);
            // 门前（大门内侧看玄関和东边的竹篱笆）、后院（水井边看薪棚和台所后门）
            yield return new Shot("ext_forecourt", Mode.Day, new Vector3(2.12f, 1.17f, -6.72f), new Vector3(6.36f, 1.06f, -.71f), 60f);
            yield return new Shot("ext_backyard", Mode.Day, new Vector3(1.06f, 1.2f, 22.27f), new Vector3(4.6f, .85f, 16.6f), 60f);
            // 隔着縁側、東縁側的玻璃拉门看主庭园
            yield return new Shot("engawa_garden", Mode.Day, new Vector3(12.73f, 1.414f, 1.485f), new Vector3(11.6f, .85f, -4.53f), 65f);
            yield return new Shot("engawaE_garden", Mode.Day, new Vector3(25.31f, 1.414f, 6.36f), new Vector3(31.47f, .71f, 7.42f), 65f);
            yield return new Shot("genkan", Mode.Interior, new Vector3(1.2f, 1.55f, 1.2f), new Vector3(7f, 1.0f, 6f));
            yield return new Shot("genkan_west", Mode.Interior, new Vector3(6.4f, 1.6f, 1.4f), new Vector3(0.4f, 1.0f, 3.6f));
            yield return new Shot("daidokoro", Mode.Interior, new Vector3(6.6f, 1.55f, 8.0f), new Vector3(1.0f, 1.0f, 13.0f));
            yield return new Shot("tsugi", Mode.Interior, new Vector3(12.3f, 2.1f, 2.4f), new Vector3(8.5f, 1.0f, 6.2f));
            yield return new Shot("butsuma", Mode.Interior, new Vector3(14.0f, 2.1f, 2.5f), new Vector3(16.8f, 1.3f, 6.9f));
            yield return new Shot("zashiki", Mode.Interior, new Vector3(23.9f, 2.15f, 4.3f), new Vector3(19.2f, 1.3f, 6.8f));
            yield return new Shot("ima", Mode.Interior, new Vector3(12.2f, 2.1f, 7.6f), new Vector3(8.5f, 1.5f, 12.0f));
            yield return new Shot("nando", Mode.Interior, new Vector3(17.5f, 2.1f, 8.0f), new Vector3(14.6f, 1.0f, 11.6f));
            yield return new Shot("oku", Mode.Interior, new Vector3(18.7f, 2.1f, 9.3f), new Vector3(23.8f, 1.2f, 10.4f));
            yield return new Shot("engawa", Mode.Interior, new Vector3(8.3f, 2.1f, 1.0f), new Vector3(24f, 1.3f, 1.0f));
            yield return new Shot("rouka", Mode.Interior, new Vector3(8.2f, 2.1f, 13.2f), new Vector3(24f, 1.3f, 13.2f));
            yield return new Shot("furo", Mode.Interior, new Vector3(11.0f, 2.1f, 15.2f), new Vector3(16.5f, 1.2f, 19.8f));
            yield return new Shot("furo_east", Mode.Interior, new Vector3(12.0f, 2.0f, 17.0f), new Vector3(17.9f, 1.1f, 17.6f));
            yield return new Shot("benjo", Mode.Interior, new Vector3(9.9f, 2.0f, 15.0f), new Vector3(8.2f, 1.0f, 19.5f));
            yield return new Shot("kura", Mode.Interior, new Vector3(8.4f, 1.85f, 25.0f), new Vector3(14.5f, 1.0f, 29.8f));
            yield return new Shot("shinshitsu", Mode.Interior, new Vector3(12.3f, 4.6f, 11.7f), new Vector3(6.0f, 3.6f, 8.0f));
            yield return new Shot("washitsu2F", Mode.Interior, new Vector3(13.6f, 4.6f, 10.7f), new Vector3(20.3f, 3.9f, 9.6f));
            yield return new Shot("kodomo", Mode.Interior, new Vector3(12.2f, 4.6f, 18.1f), new Vector3(16.6f, 3.6f, 14.7f));
            yield return new Shot("monooki", Mode.Interior, new Vector3(8.2f, 4.6f, 18.2f), new Vector3(11.4f, 3.8f, 15.0f));
            yield return new Shot("mood_zashiki", Mode.Mood, new Vector3(22.8f, 2.1f, 3.0f), new Vector3(19.5f, 1.2f, 6.8f));
            yield return new Shot("mood_rouka", Mode.Mood, new Vector3(24.0f, 2.1f, 13.2f), new Vector3(8f, 1.3f, 13.2f));
            // 门（2026-11-19）：钉死的旧门四面、居間的板戸和茶箪笥、座敷的箪笥（新坐标）
            yield return new Shot("sealed_genkan", Mode.Detail, new Vector3(7.6f, 1.6f, 2.9f), new Vector3(10.84f, 1.25f, 1.36f));
            yield return new Shot("sealed_butsuma", Mode.Detail, new Vector3(21.9f, 2.05f, 6.4f), new Vector3(20f, 1.75f, 9.93f));
            yield return new Shot("sealed_oku", Mode.Detail, new Vector3(29.2f, 2.05f, 13f), new Vector3(25.56f, 1.75f, 13.65f));
            yield return new Shot("sealed_nando", Mode.Detail, new Vector3(21.6f, 2.05f, 15.6f), new Vector3(25.4f, 1.35f, 13.65f));
            yield return new Shot("doors_ima", Mode.Detail, new Vector3(16.5f, 2.05f, 15.5f), new Vector3(11f, 1.2f, 12f));
            yield return new Shot("doors_zashiki", Mode.Detail, new Vector3(30.5f, 2.05f, 8.8f), new Vector3(26f, 1.2f, 4.5f));
        }

        [MenuItem("Hako/Art/9 场景截图检查", priority = 90)]
        public static void Shoot()
        {
            Directory.CreateDirectory(OutDir);
            var moonGo = GameObject.Find("Moonlight");
            var moon = moonGo != null ? moonGo.GetComponent<Light>() : null;
            var ambMode = RenderSettings.ambientMode;
            var ambColor = RenderSettings.ambientLight;
            var ambProbe = RenderSettings.ambientProbe;
            bool fog = RenderSettings.fog;
            float moonI = moon ? moon.intensity : 0f;
            var moonC = moon ? moon.color : Color.white;
            var moonS = moon ? moon.shadows : LightShadows.None;

            var camGo = new GameObject("ArtSnapshotCam");
            var cam = camGo.AddComponent<Camera>();
            cam.nearClipPlane = .05f;
            cam.farClipPlane = 200f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            var data = cam.GetUniversalAdditionalCameraData();
            var spotGo = new GameObject("ArtSnapshotFlashlight");
            spotGo.transform.SetParent(camGo.transform, false);
            var spot = spotGo.AddComponent<Light>();
            spot.type = LightType.Spot;
            spot.spotAngle = 80f;
            spot.innerSpotAngle = 45f;
            spot.range = 16f;
            spot.color = new Color(1f, .93f, .82f);
            spot.shadows = LightShadows.Soft;
            int count = 0;
            try
            {
                foreach (var s in Shots())
                {
                    bool plan = s.mode == Mode.Plan1F || s.mode == Mode.Plan2F;
                    cam.orthographic = plan;
                    data.renderPostProcessing = !plan;
                    spot.enabled = s.mode == Mode.Interior || s.mode == Mode.Mood || s.mode == Mode.Detail;
                    spot.intensity = s.mode == Mode.Mood ? 4f : 3f;
                    RenderSettings.fog = s.mode == Mode.Mood && fog;
                    if (moon)
                    {
                        moon.intensity = s.mode == Mode.Day ? 1.5f : plan ? .9f : moonI;
                        moon.color = s.mode == Mode.Day || plan ? new Color(1f, .97f, .92f) : moonC;
                        moon.shadows = plan ? LightShadows.None : moonS;
                    }
                    Color amb = s.mode == Mode.Day ? new Color(.36f, .38f, .42f)
                        : plan ? new Color(.6f, .6f, .6f)
                        : s.mode == Mode.Interior || s.mode == Mode.Detail ? new Color(.36f, .34f, .32f) : ambColor;
                    SetAmbient(amb);
                    cam.backgroundColor = s.mode == Mode.Day ? new Color(.45f, .52f, .62f) : new Color(.01f, .012f, .02f);

                    int w = 1600, h = plan ? 1600 : 900;
                    if (plan)
                    {
                        float k = HouseBlockoutGenerator.PlanStretch;
                        cam.orthographicSize = s.fov * k;
                        cam.nearClipPlane = .01f;
                        cam.farClipPlane = 30f;
                        var pp = new Vector3(s.pos.x * k, HouseBlockoutGenerator.PlanCutHeight(s.mode == Mode.Plan2F), s.pos.z * k);
                        camGo.transform.SetPositionAndRotation(pp, Quaternion.Euler(90f, 0f, 0f));
                    }
                    else
                    {
                        cam.fieldOfView = s.fov;
                        cam.nearClipPlane = .05f;
                        cam.farClipPlane = 300f;
                        // 机位按 V3 旧坐标书写：室内跟着房间搬家，室外整体按比例放大
                        Vector3 pos, look;
                        if (s.mode == Mode.Day)
                        {
                            float k = ExteriorScale;
                            pos = s.pos * k; look = s.look * k;
                        }
                        else if (s.mode == Mode.Detail) { pos = s.pos; look = s.look; }
                        else { pos = HouseBlockoutGenerator.MapOldPoint(s.pos); look = HouseBlockoutGenerator.MapOldPoint(s.look); }
                        camGo.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(look - pos, Vector3.up));
                    }
                    Render(cam, s.name, w, h);
                    count++;
                }
            }
            finally
            {
                RenderSettings.ambientMode = ambMode;
                RenderSettings.ambientLight = ambColor;
                RenderSettings.ambientProbe = ambProbe;
                RenderSettings.fog = fog;
                if (moon)
                {
                    moon.intensity = moonI;
                    moon.color = moonC;
                    moon.shadows = moonS;
                }
                Object.DestroyImmediate(camGo);
            }
            Debug.Log($"[Hako Art] 截图 {count} 张 → {OutDir}");
        }

        static void SetAmbient(Color c)
        {
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = c;
            var sh = new SphericalHarmonicsL2();
            sh.AddAmbientLight(c);
            RenderSettings.ambientProbe = sh;
        }

        static void Render(Camera cam, string file, int w, int h)
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
            File.WriteAllBytes(Path.Combine(OutDir, file + ".png"), tex.EncodeToPNG());
            cam.targetTexture = null;
            Object.DestroyImmediate(tex);
            rt.Release();
            Object.DestroyImmediate(rt);
        }
    }
}
