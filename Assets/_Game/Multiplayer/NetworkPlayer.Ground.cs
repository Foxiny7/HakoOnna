using Unity.Netcode;
using UnityEngine;

namespace HakoOnna.Multiplayer
{
    public sealed partial class NetworkPlayer
    {
        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        internal void DropGroundRpc()
        {
            if (searching >= 0 || !explorer.Inventory[selected.Value].HasValue) return;
            var origin = transform.position + Vector3.up * .65f;
            var forward = transform.forward;
            const float distance = .65f;
            if (Physics.SphereCast(origin, .15f, forward, out _, distance,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) ||
                !Physics.Raycast(origin + forward * distance, Vector3.down, out var floor, 1.2f,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) ||
                floor.normal.y < .7f || floor.collider.GetComponentInParent<NetworkPlayer>()) return;

            if (!explorer.TryDrop(selected.Value, out var kind)) return;
            var item = Instantiate(session.GroundPrefab, floor.point + Vector3.up * .05f,
                transform.rotation).GetComponent<GroundItem>();
            item.DroppedKind = kind;
            item.NetworkObject.Spawn();
            SyncGroundSlot(selected.Value);
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        internal void TakeGroundRpc(ulong id, Vector3 direction)
        {
            if (searching >= 0 || !float.IsFinite(direction.sqrMagnitude) ||
                Mathf.Abs(direction.sqrMagnitude - 1) > .01f) return;
            LookTarget(direction, out _, out var ground);
            if (!ground || !ground.IsSpawned || ground.NetworkObjectId != id) return;
            int slot = explorer.Receive(ground.Kind.Value, selected.Value);
            if (slot < 0) return;
            ground.NetworkObject.Despawn();
            SyncGroundSlot(slot);
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        internal void SeedExchangeTest(bool full)
        {
            var kind = full ? HakoOnna.Rules.ItemKind.Diary : HakoOnna.Rules.ItemKind.GateKey;
            int slot = explorer.Receive(kind, 1);
            SyncGroundSlot(slot);
            if (full)
                for (int i = 2; i < slots.Count; i++)
                {
                    explorer.Receive(HakoOnna.Rules.ItemKind.Pliers, i);
                    slots[i] = (int)HakoOnna.Rules.ItemKind.Pliers;
                }
        }

        internal int TestHeld => heldItem.Value;
        internal int TestOccupied
        {
            get
            {
                int count = 0;
                foreach (int item in slots) if (item >= 0) count++;
                return count;
            }
        }
#endif

        private void SyncGroundSlot(int slot)
        {
            slots[slot] = (int?)explorer.Inventory[slot] ?? -1;
            selected.Value = slot;
            heldItem.Value = slots[slot];
            lampOn.Value = false;
        }
    }
}
