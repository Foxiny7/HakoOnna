using UnityEngine;

namespace HakoOnna.Multiplayer
{
    internal sealed class HouseAmbience : MonoBehaviour
    {
        private HouseFoley house;
        private AudioSource indoor, yard;
        private float outside, nextCheck;
        private bool inYard;

        internal void Begin(HouseFoley foley)
        {
            house = foley;
            indoor = Loop("Indoor");
            yard = Loop("Yard");
        }

        private AudioSource Loop(string name)
        {
            var source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = true;
            source.volume = 0;
            source.clip = Resources.Load<AudioClip>("Foley/" + name);
            source.Play();
            return source;
        }

        private void Update()
        {
            if (Time.unscaledTime >= nextCheck)
            {
                nextCheck = Time.unscaledTime + .15f;
                inYard = !house.IsInside(transform.position);
            }
            outside = Mathf.MoveTowards(outside, inYard ? 1 : 0, Time.unscaledDeltaTime * .7f);
            indoor.volume = .12f * (1 - outside);
            yard.volume = .14f * outside;
        }
    }
}
