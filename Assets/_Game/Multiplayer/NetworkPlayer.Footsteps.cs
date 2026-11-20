using HakoOnna.Rules;
using Unity.Netcode;
using UnityEngine;

namespace HakoOnna.Multiplayer
{
    public sealed partial class NetworkPlayer
    {
        private AudioSource footsteps;
        private float distanceToStep = .12f;
        private bool leftFoot;
        private bool OwnerWalking => move.sqrMagnitude > 0 || (requested >= 0 && duration == 0);

        private void TickFootsteps(Vector3 displacement, bool grounded, bool running)
        {
            displacement.y = 0;
            if (!grounded || explorer.Role != Role.Explorer || displacement.sqrMagnitude < .000001f)
            {
                distanceToStep = .12f;
                return;
            }
            distanceToStep -= displacement.magnitude;
            if (distanceToStep > 0) return;
            distanceToStep += running ? 1.50f : 1.12f;
            leftFoot = !leftFoot;
            FootstepRpc(session.Foley.SurfaceAt(transform.position), leftFoot, running);
        }

        [Rpc(SendTo.ClientsAndHost, Delivery = RpcDelivery.Unreliable)]
        private void FootstepRpc(FootSurface surface, bool left, bool running)
        {
            if (IsOwner && !OwnerWalking) return;
            if (!footsteps) footsteps = HouseFoley.Source(gameObject, 1f);
            footsteps.pitch = left ? .96f : 1.04f;
            footsteps.volume = running ? .30f : .14f;
            footsteps.clip = session.Foley.Step(surface, left);
            footsteps.Play();
        }
    }
}
