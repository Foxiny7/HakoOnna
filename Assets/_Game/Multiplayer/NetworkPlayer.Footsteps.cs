using HakoOnna.Rules;
using Unity.Netcode;
using UnityEngine;

namespace HakoOnna.Multiplayer
{
    public sealed partial class NetworkPlayer
    {
        private AudioSource footsteps;
        private float distanceToStep = .55f;
        private bool leftFoot;

        private void TickFootsteps(Vector3 displacement, bool grounded, bool running)
        {
            displacement.y = 0;
            if (!grounded || explorer.Role != Role.Explorer || displacement.sqrMagnitude < .000001f)
            {
                distanceToStep = .55f;
                return;
            }
            distanceToStep -= displacement.magnitude;
            if (distanceToStep > 0) return;
            distanceToStep += running ? 1.65f : 1.25f;
            leftFoot = !leftFoot;
            FootstepRpc(session.Foley.SurfaceAt(transform.position), leftFoot, running);
        }

        [Rpc(SendTo.ClientsAndHost, Delivery = RpcDelivery.Unreliable)]
        private void FootstepRpc(FootSurface surface, bool left, bool running)
        {
            if (!footsteps) footsteps = HouseFoley.Source(gameObject, .68f);
            footsteps.pitch = left ? .96f : 1.04f;
            footsteps.PlayOneShot(session.Foley.Step(surface), running ? 1f : .78f);
        }
    }
}
