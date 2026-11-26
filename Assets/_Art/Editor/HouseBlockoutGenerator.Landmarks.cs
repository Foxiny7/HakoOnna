using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace HakoOnna.Art.EditorTools
{
    /// <summary>
    /// 2026-11-22 检查后补的两样：
    /// <list type="bullet">
    /// <item><b>认路的微光</b>：宽 10.9 m 的大房间（玄関、台所、風呂、土蔵、二楼两间）比雾里看得见的距离（7.5 m）还宽，
    ///   站在门口看不到对面，房间中间一片黑。每间在吊灯的位置加一盏朝下的、快灭了的暖光，只照亮灯下一小圈地面，
    ///   让玩家知道“房间中间在那儿”。照不到墙，不改变整体的暗度。
    ///   2026-11-26 起投软阴影：原来不投影，光穿过灯下的桌子照到桌底的地面，桌子像浮着。</item>
    /// <item><b>沿墙放不下的三件大件摆到房间中间</b>：手压泵（台所）、糸車（納戸）、木马（子供部屋）。</item>
    /// </list>
    /// </summary>
    public static partial class HouseBlockoutGenerator
    {
        static readonly string[] LandmarkRooms = { "Genkan", "Daidokoro", "Furo", "Kura", "Shinshitsu", "Washitsu2F" };

        static void BuildLandmarkLights(Transform lighting)
        {
            var parent = ArtKit.Empty(lighting, "Landmarks", Vector3.zero);
            foreach (var room in DRooms)
            {
                if (System.Array.IndexOf(LandmarkRooms, room.id) < 0) continue;
                float floor = FloorY(room);
                var go = new GameObject("Landmark_" + room.id);
                go.transform.SetParent(parent, false);
                go.transform.position = new Vector3(room.r.center.x, Mathf.Min(CeilY(room) - .9f, floor + 2.3f), room.r.center.y);
                go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                var light = go.AddComponent<Light>();
                light.type = LightType.Spot;
                light.color = new Color(1f, .6f, .3f);
                light.intensity = 4f;
                light.range = 3.4f;       // 灯下 2.3 m 是地面；照不到墙，也漏不到楼下
                light.spotAngle = 115f;
                light.innerSpotAngle = 50f;
                // 2026-11-26：投软阴影（见上）。阴影图用中档（512）：灯下只有 3.4 m，够用，也不会挤占手电的阴影图
                light.shadows = LightShadows.Soft;
                var data = new SerializedObject(light.GetUniversalAdditionalLightData());
                data.FindProperty("m_AdditionalLightsShadowResolutionTier").intValue = UniversalAdditionalLightData.AdditionalLightsShadowResolutionTierMedium;
                data.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        /// <summary>把“沿墙摆的一件”（局部原点在背面中点、+z 朝外）包成中央家具组（局部原点在占地中心）。</summary>
        static CenterSet CFromPiece(string name, float w, float d, float h, float blockH, System.Action<Kit> draw)
        {
            var set = new CenterSet { name = name, w = w, d = d, h = h };
            set.draw = (k, r) =>
            {
                k.Push(new Vector3(0, 0, -d / 2));
                draw(k);
                k.Pop();
            };
            set.blocks.Add((Vector3.zero, new Vector3(w, blockH, d)));
            return set;
        }

        static CenterSet CPump() => CFromPiece("HandPump", 1.25f, .7f, 1.2f, 1.0f, DrawPump);
        static CenterSet CSpinningWheel() => CFromPiece("SpinningWheel", 1.0f, .42f, .85f, .8f, DrawSpinningWheel);
        static CenterSet CRockingHorse() => CFromPiece("RockingHorse", 1.1f, .45f, .95f, .8f, DrawRockingHorse);
    }
}
