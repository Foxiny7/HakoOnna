using Unity.Netcode;
using UnityEngine;

namespace HakoOnna.Multiplayer
{
    public sealed partial class NetworkPlayer
    {
        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        private void UseDoorRpc(int id, Vector3 direction)
        {
            if (searching >= 0 || id < 0 || id >= session.Doors.Length ||
                !float.IsFinite(direction.sqrMagnitude) || Mathf.Abs(direction.sqrMagnitude - 1) > .01f) return;
            LookTarget(direction, out var target);
            if (target != id) return;
            var door = session.Doors[id];
            if (door.IsMoving) return;
            if (door.Locked) door.Rattle();
            else
            {
                if (door.IsOpen)
                    foreach (var peer in NetworkManager.ConnectedClientsList)
                        if (peer.PlayerObject && door.WorldClosedVolume.Intersects(
                            peer.PlayerObject.GetComponent<CharacterController>().bounds)) return;
                door.IsOpen = !door.IsOpen;
            }
            DoorChangedRpc(id, door.IsOpen);
        }

        [Rpc(SendTo.NotServer)]
        private void DoorChangedRpc(int id, bool open)
        {
            var door = session.Doors[id];
            if (door.Locked) door.Rattle();
            else door.IsOpen = open;
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        private void RequestDoorsRpc()
        {
            SyncDoorsRpc(System.Array.ConvertAll(session.Doors, door => door.IsOpen));
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        internal void TryTestDoor(HakoOnna.Art.DoorVisual door)
        {
            var leaf = door.GetComponentInChildren<BoxCollider>();
            var direction = (leaf.bounds.center - transform.position - Vector3.up * config.EyeHeight).normalized;
            LookTarget(direction, out int id);
            if (id >= 0 && session.Doors[id] == door && door.IsOpen)
                UseDoorRpc(id, direction);
        }
#endif

        [Rpc(SendTo.Owner)]
        private void SyncDoorsRpc(bool[] open)
        {
            for (int i = 0; i < open.Length; i++)
                session.Doors[i].SetOpenImmediate(open[i]);
        }
    }
}
