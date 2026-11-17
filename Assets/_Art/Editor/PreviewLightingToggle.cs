using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace HakoOnna.Art.EditorTools
{
    /// <summary>
    /// 在“明亮预览”和“真实夜晚”两种环境光之间切换，方便在编辑器里检查灰盒布局。
    /// 只改环境光和雾，不改月光和后处理。保存场景前请切回“真实夜晚”。
    /// </summary>
    internal static class PreviewLightingToggle
    {
        static readonly Color NightAmbient = new Color(0.025f, 0.03f, 0.045f);
        static readonly Color PreviewAmbient = new Color(0.6f, 0.6f, 0.6f);

        [MenuItem("Hako/Art/3 切换预览照明（明亮 ↔ 真实夜晚）", priority = 40)]
        static void Toggle()
        {
            bool isNight = RenderSettings.ambientLight.maxColorComponent < 0.2f;
            RenderSettings.ambientMode = AmbientMode.Flat;
            if (isNight)
            {
                RenderSettings.ambientLight = PreviewAmbient;
                RenderSettings.fog = false;
                Debug.Log("[Hako Art] 已切换到明亮预览。保存场景前请再执行一次切回真实夜晚。");
            }
            else
            {
                RenderSettings.ambientLight = NightAmbient;
                RenderSettings.fog = true;
                Debug.Log("[Hako Art] 已切回真实夜晚照明。");
            }
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            SceneView.RepaintAll();
        }
    }
}
