using UnityEngine;

namespace HakoOnna.Art
{
    /// <summary>
    /// 挂在不属于门扇的碰撞体上（縁側玻璃拉门的 Window_Glass），指向对应的门，
    /// 这样 DoorVisual.FromCollider 也能认出它。不含逻辑。
    /// </summary>
    [DisallowMultipleComponent]
    public class DoorLink : MonoBehaviour
    {
        [SerializeField] DoorVisual door;

        public DoorVisual Door => door;

        public void Bind(DoorVisual target) => door = target;
    }
}
