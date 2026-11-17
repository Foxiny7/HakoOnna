using UnityEngine;
using UnityEngine.Rendering;

namespace HakoOnna.Art
{
    /// <summary>
    /// 猎杀阶段的画面氛围。程序在猎杀开始和结束时调用 SetHunt(true/false)。
    /// 只影响画面，不包含任何玩法逻辑。
    /// </summary>
    public class HuntAtmosphere : MonoBehaviour
    {
        [SerializeField] Volume huntVolume;
        [SerializeField] float fadeInSeconds = 1.5f;
        [SerializeField] float fadeOutSeconds = 3f;

        float target;

        public bool IsHunting => target > 0.5f;

        public void SetHunt(bool active) => target = active ? 1f : 0f;

        public void Bind(Volume volume) => huntVolume = volume;

        void Update()
        {
            if (huntVolume == null) return;
            float seconds = target > huntVolume.weight ? fadeInSeconds : fadeOutSeconds;
            float speed = 1f / Mathf.Max(0.01f, seconds);
            huntVolume.weight = Mathf.MoveTowards(huntVolume.weight, target, speed * Time.deltaTime);
        }
    }
}
