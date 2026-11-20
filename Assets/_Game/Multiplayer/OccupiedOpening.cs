using System.Collections;
using System.Linq;
using HakoOnna.Art;
using UnityEngine;

namespace HakoOnna.Multiplayer
{
    internal static class OccupiedOpening
    {
        // Capture the authored hinge/slide endpoints, then animate locally without exposing inventory.
        internal static IEnumerator Play(ContainerVisual box, Transform victim)
        {
            box.PlayGrab();
            var parts = box.GetComponentsInChildren<Transform>();
            box.SetOpenImmediate(false);
            var closedPos = parts.Select(t => t.localPosition).ToArray();
            var closedRot = parts.Select(t => t.localRotation).ToArray();
            box.SetOpenImmediate(true);
            var openPos = parts.Select(t => t.localPosition).ToArray();
            var openRot = parts.Select(t => t.localRotation).ToArray();
            box.SetOpenImmediate(false);
            var bounds = box.GetComponentInChildren<Collider>().bounds;
            var prop = Object.Instantiate(Resources.Load<GameObject>("HakoOnnaPlayer").transform.Find("Body").gameObject);
            prop.SetActive(true);
            float height = Mathf.Min(bounds.size.y * .78f, 1.25f);
            prop.transform.localScale = new Vector3(.3f, height * .5f, .3f);
            var glow = prop.AddComponent<Light>();
            glow.color = new Color(1,.22f,.12f);
            glow.range = 3;
            glow.intensity = 4;
            var start = bounds.center;
            var end = Vector3.Lerp(start, victim.position + Vector3.up * 1.3f, .75f);
            var foley = box.GetComponent<ContainerFoley>();
            Transform shriek = null;
            bool lunged = false, closing = false;
            try
            {
                for (float t = 0; t < 1.4f && box && !box.IsOpen; t += Time.deltaTime)
                {
                    if (!lunged && t >= .12f)
                    {
                        lunged = true;
                        shriek = foley?.Shriek(prop.transform);
                    }
                    if (!closing && t >= .95f)
                    {
                        closing = true;
                        foley?.Close();
                    }
                    float open = Mathf.SmoothStep(0, 1, t / .22f) * (1 - Mathf.SmoothStep(0, 1, (t - .95f) / .45f));
                    for (int i = 0; i < parts.Length; i++)
                    {
                        if (closedPos[i] != openPos[i]) parts[i].localPosition = Vector3.Lerp(closedPos[i], openPos[i], open);
                        if (closedRot[i] != openRot[i]) parts[i].localRotation = Quaternion.Slerp(closedRot[i], openRot[i], open);
                    }
                    float lunge = Mathf.SmoothStep(0, 1, (t - .12f) / .24f) * (1 - Mathf.SmoothStep(0, 1, (t - .73f) / .32f));
                    prop.transform.position = Vector3.Lerp(start, end, lunge);
                    yield return null;
                }
            }
            finally
            {
                if (box && !box.IsOpen) box.SetOpenImmediate(false);
                if (shriek) shriek.SetParent(null, true);
                Object.Destroy(prop);
            }
        }
    }
}
