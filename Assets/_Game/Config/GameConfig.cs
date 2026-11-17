using HakoOnna.Rules;
using UnityEngine;

namespace HakoOnna
{
    [CreateAssetMenu(menuName = "Hako Onna/Game Config", fileName = "GameConfig")]
    public sealed class GameConfig : ScriptableObject
    {
        [SerializeField, Min(0.01f)] private float searchSeconds = 2f;
        [SerializeField, Min(0f)] private float transferCooldownSeconds = 3f;

        public RulesSettings ToRulesSettings()
        {
            return new RulesSettings(searchSeconds, transferCooldownSeconds);
        }
    }
}
