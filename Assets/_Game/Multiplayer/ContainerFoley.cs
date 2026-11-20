using HakoOnna.Art;
using UnityEngine;

namespace HakoOnna.Multiplayer
{
    internal sealed class ContainerFoley : MonoBehaviour
    {
        private AudioSource source;
        private AudioClip opened, closed, shriek;

        internal void Bind(ContainerVisual container, string kind)
        {
            source = HouseFoley.Source(gameObject, .75f);
            opened = Resources.Load<AudioClip>("Foley/Open_" + kind);
            closed = Resources.Load<AudioClip>("Foley/Close_" + kind);
            shriek = Resources.Load<AudioClip>("Foley/HiderShriek");
            container.Opened += Open;
            container.GrabPlayed += Open;
            container.Closed += Close;
        }

        internal void Shriek()
        {
            source.PlayOneShot(shriek, .82f);
        }

        private void Open() => Play(opened);
        internal void Close() => Play(closed);

        private void Play(AudioClip clip)
        {
            source.clip = clip;
            source.Play();
        }
    }
}
