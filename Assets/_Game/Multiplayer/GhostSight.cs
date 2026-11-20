using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace HakoOnna.Multiplayer
{
    // Instantiated only for the owning hider's viewport.
    internal sealed class GhostSight
    {
        private readonly LocalSession session;
        private readonly Camera camera;
        private readonly Dictionary<Renderer, Material[]> originals = new();
        private readonly Material mist, reachable, selected, self;
        private readonly Light fill;
        private readonly Texture2D peripheral;
        private readonly bool fog = RenderSettings.fog;
        private readonly AmbientMode mode = RenderSettings.ambientMode;
        private readonly Color ambient = RenderSettings.ambientLight;
        private readonly SphericalHarmonicsL2 probe = RenderSettings.ambientProbe;
        private readonly Renderer body;
        private readonly Material skin;
        private readonly Bounds[] boxes;
        private readonly List<(float depth, int id)> hits = new();

        internal GhostSight(Camera camera, Transform avatar, LocalSession session)
        {
            this.camera = camera;
            this.session = session;
            boxes = System.Array.ConvertAll(session.Containers, c => c.GetComponentInChildren<Collider>().bounds);
            camera.GetUniversalAdditionalCameraData().renderPostProcessing = false;
            RenderSettings.fog = false;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(.66f, .92f, .84f);
            var lightProbe = new SphericalHarmonicsL2();
            lightProbe.AddAmbientLight(RenderSettings.ambientLight);
            RenderSettings.ambientProbe = lightProbe;
            fill = camera.gameObject.AddComponent<Light>();
            fill.type = LightType.Point;
            fill.range = 20; fill.intensity = 5;
            fill.color = new Color(.67f, 1f, .86f);
            var shader = Resources.Load<Shader>("GhostSilhouette");
            mist = Make(new Color(.46f,.91f,.80f,.36f), 3070);
            reachable = Make(new Color(.08f,.88f,.78f,.28f), 3060);
            selected = Make(new Color(1f,.66f,.19f,.68f), 3080);
            self = Make(new Color(1f,.08f,.16f,.91f), 3090);
            self.SetFloat("_Solid", .9f);
            peripheral = new Texture2D(128, 80, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color[128 * 80];
            for (int y = 0; y < 80; y++)
                for (int x = 0; x < 128; x++)
                {
                    float u = (x / 127f - .5f) * 2, v = (y / 79f - .5f) * 2;
                    float edge = Mathf.Clamp01((u*u + .82f*v*v - .62f) / .95f);
                    float vein = Mathf.PerlinNoise(u * 5 + v * 2, v * 10 + 12);
                    pixels[y * 128 + x] = new Color(.07f + .19f*vein, .23f, .26f + .12f*vein,
                        edge * edge * (.28f + .36f*vein));
                }
            peripheral.SetPixels(pixels);
            peripheral.Apply(false, true);
            body = avatar.GetComponent<Renderer>();
            skin = body.sharedMaterial;
            body.sharedMaterial = self;

            Material Make(Color color, int queue)
            {
                var mat = new Material(shader) { renderQueue = queue };
                mat.SetColor("_Tint", color);
                return mat;
            }
        }

        internal void DrawPeriphery()
        {
            if (Event.current.type == EventType.Repaint)
                GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), peripheral);
        }

        // A reachable box behind the camera still has a directional edge marker.
        private bool EdgeMarker(int id, out Vector2 pixel)
        {
            Vector3 projected = camera.WorldToViewportPoint(boxes[id].center);
            var dir = new Vector2(projected.x - .5f, projected.y - .5f);
            if (projected.z > 0 && Mathf.Abs(dir.x) < .46f && Mathf.Abs(dir.y) < .43f)
            {
                pixel = default;
                return false;
            }
            if (projected.z < 0) dir = -dir;
            if (dir.sqrMagnitude < .00001f) dir = Vector2.down;
            float scale = Mathf.Min(.46f / Mathf.Max(.0001f, Mathf.Abs(dir.x)),
                .43f / Mathf.Max(.0001f, Mathf.Abs(dir.y)));
            pixel = Vector2.Scale(Vector2.one * .5f + dir * scale, new Vector2(Screen.width, Screen.height));
            return true;
        }

        internal void DrawMarkers(int current, int choice)
        {
            var color = GUI.color;
            for (int i = 0; i < boxes.Length; i++)
            {
                if (!session.Routes.Allows(current, i) || !EdgeMarker(i, out var pixel)) continue;
                GUI.color = i == choice ? new Color(1,.75f,.28f) : new Color(.50f,1,.88f);
                var metres = Mathf.RoundToInt(Vector3.Distance(boxes[current].center, boxes[i].center));
                GUI.Box(new Rect(pixel.x - 19, Screen.height - pixel.y - 10, 38, 21), metres + "m");
            }
            GUI.color = color;
        }

        internal void Show(int current, int target = -1)
        {
            Restore();
            for (int i = 0; i < boxes.Length; i++)
            {
                if (i != current && !session.Routes.Allows(current, i)) continue;
                var material = i == current ? mist : i == target ? selected : reachable;
                foreach (var r in session.Containers[i].GetComponentsInChildren<MeshRenderer>())
                {
                    if (!r.enabled) continue;
                    var materials = r.sharedMaterials;
                    originals.Add(r, materials);
                    var replacement = new Material[materials.Length];
                    System.Array.Fill(replacement, material);
                    r.sharedMaterials = replacement;
                }
            }
        }

        internal int Pick(Vector2 pointer, int current, int previous, bool cycle)
        {
            var ray = camera.ScreenPointToRay(pointer);
            hits.Clear();
            for (int i = 0; i < boxes.Length; i++)
            {
                if (!session.Routes.Allows(current,i)) continue;
                if (EdgeMarker(i, out var pixel) && Mathf.Abs(pointer.x-pixel.x) <= 19 && Mathf.Abs(pointer.y-pixel.y) <= 11)
                    hits.Add((0,i));
                else if (boxes[i].IntersectRay(ray, out float depth) && depth >= 0)
                    hits.Add((depth,i));
            }
            if (hits.Count == 0) return -1;
            hits.Sort();
            if (cycle)
                for (int i = 0; i < hits.Count; i++)
                    if (hits[i].id == previous) return hits[(i + 1) % hits.Count].id;
            return hits[0].id;
        }

        private void Restore()
        {
            foreach (var pair in originals) if (pair.Key) pair.Key.sharedMaterials = pair.Value;
            originals.Clear();
        }

        internal void Dispose()
        {
            Restore();
            if (body) body.sharedMaterial = skin;
            RenderSettings.ambientMode = mode;
            RenderSettings.ambientLight = ambient;
            RenderSettings.ambientProbe = probe;
            RenderSettings.fog = fog;
            Object.Destroy(peripheral);
            Object.Destroy(fill);
            foreach (var m in new[] { mist, reachable, selected, self }) Object.Destroy(m);
        }
    }
}
