using UnityEngine;

namespace HakoOnna.Multiplayer
{
    internal static class ZashikiArt
    {
        private const string Path = "Zashiki/";

        internal static void DressAlcove(Transform alcove)
        {
            alcove.Find("DisplayPlatform").GetComponent<Renderer>().sharedMaterial = Resources.Load<Material>(Path + "PlatformWood");
            alcove.Find("InkPanel").GetComponent<Renderer>().enabled = false;
            var scroll = alcove.Find("HangingScroll");
            scroll.GetComponent<Renderer>().sharedMaterial = Resources.Load<Material>(Path + "MistyLandscape");
            var scale = scroll.localScale;
            scale.x = scale.y * (549f / 1085f);
            scroll.localScale = scale;
            var standIn = alcove.Find("Vase");
            standIn.GetComponent<Renderer>().enabled = false;
            var pot = DisplayMesh(alcove, "Pottery", Resources.Load<Material>(Path + "PorcelainFinish"));
            var bounds = pot.sharedMesh.bounds;
            float factor = standIn.localScale.y * 2 / bounds.size.y;
            pot.transform.localScale = Vector3.one * factor;
            pot.transform.localPosition = standIn.localPosition - Vector3.up * standIn.localScale.y
                - new Vector3(bounds.center.x, bounds.min.y, bounds.center.z) * factor;
        }

        internal static void DressChest(Transform visual, Transform drawer)
        {
            foreach (var renderer in visual.GetComponentsInChildren<MeshRenderer>())
                renderer.enabled = false;
            var finish = Resources.Load<Material>(Path + "ChestFinish");
            var chest = DisplayMesh(visual, "Chest", finish);
            var tray = DisplayMesh(drawer, "LowerDrawer", finish);
            var original = chest.sharedMesh.bounds;
            var volume = visual.Find("Collision").GetComponent<BoxCollider>();
            var size = volume.size;
            var scale = new Vector3(size.x / original.size.x, size.y / original.size.y, size.z / original.size.z);
            chest.transform.localScale = tray.transform.localScale = scale;
            chest.transform.localPosition = volume.center - Vector3.Scale(original.center, scale);
            tray.transform.localPosition = chest.transform.localPosition - drawer.localPosition;
            var interior = tray.sharedMesh.bounds;
            drawer.Find("ItemAnchor").localPosition = tray.transform.localPosition + Vector3.Scale(scale,
                new Vector3(interior.center.x, interior.min.y + .045f, interior.center.z));
            drawer.Find("Handle").localPosition = tray.transform.localPosition + Vector3.Scale(scale,
                new Vector3(interior.center.x, interior.center.y, interior.max.z));
        }

        private static MeshFilter DisplayMesh(Transform parent, string name, Material material)
        {
            var part = new GameObject(name);
            part.transform.SetParent(parent, false);
            var filter = part.AddComponent<MeshFilter>();
            filter.sharedMesh = Resources.Load<GameObject>(Path + name).GetComponentInChildren<MeshFilter>().sharedMesh;
            part.AddComponent<MeshRenderer>().sharedMaterial = material;
            return filter;
        }
    }
}
