using System.Collections.Generic;
using HakoOnna.Art;
using UnityEngine;

namespace HakoOnna.Multiplayer
{
    internal enum FootSurface : byte { Wood, Tatami, Stone, Tile, Earth, Gravel }

    internal sealed class HouseFoley
    {
        private readonly List<(Bounds bounds, FootSurface surface)> floors = new();
        private readonly List<Bounds> rooms = new();
        private readonly AudioClip[,] steps = new AudioClip[6, 2];

        public HouseFoley(Transform house)
        {
            foreach (FootSurface surface in System.Enum.GetValues(typeof(FootSurface)))
            {
                steps[(int)surface, 0] = Resources.Load<AudioClip>("Foley/Step_" + surface);
                steps[(int)surface, 1] = Resources.Load<AudioClip>("Foley/Step_" + surface + "_B");
            }

            foreach (var renderer in house.Find("Structure").GetComponentsInChildren<MeshRenderer>())
            {
                FootSurface surface;
                if (renderer.name == "Floor" && renderer.transform.parent.name.StartsWith("Room_"))
                {
                    rooms.Add(renderer.bounds);
                    surface = renderer.sharedMaterial.name switch
                    {
                        "M_Tatami" => FootSurface.Tatami,
                        "M_Tile_Bath" => FootSurface.Tile,
                        "M_Stone_Doma" when !renderer.transform.parent.name.StartsWith("Room_Kura_") => FootSurface.Stone,
                        _ => FootSurface.Wood
                    };
                }
                else if (renderer.name == "ShoeStone") surface = FootSurface.Stone;
                else if (renderer.name.StartsWith("Step_") || renderer.name.StartsWith("ThresholdStep")) surface = FootSurface.Wood;
                else continue;
                floors.Add((renderer.bounds, surface));
            }
        }

        public AudioClip Step(FootSurface surface, bool left = true) => steps[(int)surface, left ? 0 : 1];

        public bool IsInside(Vector3 listener)
        {
            foreach (var room in rooms)
            {
                float height = listener.y - room.max.y;
                if (height >= -.15f && height < 4f && listener.x >= room.min.x && listener.x <= room.max.x &&
                    listener.z >= room.min.z && listener.z <= room.max.z) return true;
            }
            return false;
        }

        public FootSurface SurfaceAt(Vector3 feet)
        {
            var best = .35f;
            var result = FootSurface.Earth;
            foreach (var (bounds, surface) in floors)
            {
                var distance = Mathf.Abs(feet.y - bounds.max.y);
                if (distance >= best || feet.x < bounds.min.x || feet.x > bounds.max.x ||
                    feet.z < bounds.min.z || feet.z > bounds.max.z) continue;
                best = distance;
                result = surface;
            }
            if (best < .35f) return result;
            // The two raked gravel beds use the garden generator's outdoor scale.
            var p = new Vector2(feet.x, feet.z) * (1.41421356f / 2f);
            if (Mathf.Abs(feet.y) < .35f && (Rect.MinMaxRect(2.85f, -7.6f, 4.87f, -.04f).Contains(p) ||
                Rect.MinMaxRect(14.6f, -6.9f, 19.4f, -3.9f).Contains(p))) return FootSurface.Gravel;
            return result;
        }

        public static AudioSource Source(GameObject emitter, float volume)
        {
            var source = emitter.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 1;
            source.dopplerLevel = 0;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = 1.25f;
            source.maxDistance = 12f;
            source.volume = volume;
            return source;
        }

        public static void Bind(ContainerVisual container)
        {
            var kind = container.transform.parent.name.Split('_')[1];
            container.gameObject.AddComponent<ContainerFoley>().Bind(container, kind);
        }
    }
}
