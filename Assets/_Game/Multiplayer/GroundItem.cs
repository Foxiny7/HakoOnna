using HakoOnna.Rules;
using Unity.Netcode;
using UnityEngine;

namespace HakoOnna.Multiplayer
{
    public sealed class GroundItem : NetworkBehaviour
    {
        public readonly NetworkVariable<ItemKind> Kind = new();

        internal ItemKind DroppedKind;

        public override void OnNetworkSpawn()
        {
            if (IsServer) Kind.Value = DroppedKind;
            bool flashlight = Kind.Value == ItemKind.Flashlight;
            transform.Find("Flashlight").gameObject.SetActive(flashlight);
            transform.Find("Parcel").gameObject.SetActive(!flashlight);
        }
    }
}
