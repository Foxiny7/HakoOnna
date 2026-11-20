using Unity.Netcode;
using UnityEngine;

namespace HakoOnna.Multiplayer
{
    public sealed partial class NetworkPlayer
    {
        // Only a coarse bearing and loudness leave the server. No destination or world position is sent.
        internal void HearRoll(Vector3 origin)
        {
            var delta = origin - transform.position;
            float distanceFraction = delta.magnitude / config.RollHearingRange;
            if (distanceFraction > 1f) return;
            int sector = Mathf.RoundToInt(Mathf.Atan2(delta.x, delta.z) * Mathf.Rad2Deg / 45f);
            PlayRollRpc((byte)((sector + 8) % 8), (byte)(distanceFraction < 1f / 3 ? 3 : distanceFraction < 2f / 3 ? 2 : 1));
        }

        [Rpc(SendTo.Owner)]
        private void PlayRollRpc(byte sector, byte level)
        {
            var bearing = Quaternion.Euler(0, sector * 45, 0) * Vector3.forward;
            var clip = Resources.Load<AudioClip>("HiderRoll");
            AudioSource.PlayClipAtPoint(clip, playerCamera.transform.position + bearing, level / 3f);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            ReceivedRolls++;
            LastRollSector = sector;
#endif
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        internal int ReceivedRolls { get; private set; }
        internal byte LastRollSector { get; private set; }
#endif
    }
}
