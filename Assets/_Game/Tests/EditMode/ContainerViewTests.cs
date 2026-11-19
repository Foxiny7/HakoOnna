using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HakoOnna.Art;
using HakoOnna.Multiplayer;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HakoOnna.Tests
{
    public class ContainerViewTests
    {
        private delegate bool MeshHit(Ray ray, Mesh mesh, Matrix4x4 matrix, out RaycastHit hit);
        private static readonly MeshHit Intersect = (MeshHit)typeof(HandleUtility)
            .GetMethod("IntersectRayMesh", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
            .CreateDelegate(typeof(MeshHit));

        [Test]
        public void OpenContainersExposeTheirItemFromTheInspectionPoint()
        {
            var house = EditorSceneManager.OpenPreviewScene(LocalSession.HouseScene);
            try
            {
                var root = house.GetRootGameObjects().Single(go => go.name == "House_Blockout");
                var boxes = root.GetComponentsInChildren<ContainerVisual>();
                foreach (var box in boxes)
                {
                    var cabinet = box.transform.parent;
                    // Runtime instances can move tray children. Unpack only this preview copy.
                    if (PrefabUtility.IsOutermostPrefabInstanceRoot(cabinet.gameObject))
                        PrefabUtility.UnpackPrefabInstance(cabinet.gameObject, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                    LocalSession.ConfigureDrawer(box);
                }
                ZashikiArt.DressAlcove(root.transform.Find("Props/Zashiki_Tokonoma"));
                var meshes = root.GetComponentsInChildren<MeshFilter>().Where(m => m.sharedMesh &&
                    m.TryGetComponent<MeshRenderer>(out var renderer) && renderer.enabled).ToArray();
                var failures = new List<string>();
                var config = AssetDatabase.LoadAssetAtPath<GameConfig>("Assets/_Game/Config/GameConfig.asset");
                foreach (var box in boxes)
                {
                    var cabinet = box.transform.parent;
                    var anchor = cabinet.GetComponentsInChildren<Transform>().Single(t => t.name == "ItemAnchor");
                    var eye = cabinet.Find("InteractPoint").position + Vector3.up * config.EyeHeight;
                    var before = box.GetComponentsInChildren<MeshRenderer>().ToDictionary(m => m, m => m.localToWorldMatrix);
                    typeof(ContainerVisual).GetMethod("Apply", BindingFlags.Instance | BindingFlags.NonPublic)
                        .Invoke(box, new object[] { .5f });
                    var halfway = before.Where(pair => pair.Value != pair.Key.localToWorldMatrix)
                        .ToDictionary(pair => pair.Key, pair => pair.Key.localToWorldMatrix);
                    Assert.IsNotEmpty(halfway, cabinet.name);
                    box.SetOpenImmediate(true);
                    Assert.IsTrue(halfway.All(pair => pair.Value != pair.Key.localToWorldMatrix), cabinet.name);
                    var moving = box.transform.Find("Drawer_2") ?? box.transform.Find("Lid_Pivot")
                        ?? box.transform.Find("Door_R_Pivot") ?? box.transform.Find("Door_Pivot") ?? box.transform.Find("Fusuma_R");
                    var camera = NetworkPlayer.InspectionPosition(eye, anchor.position + Vector3.up * .08f,
                        anchor, moving, 1, house.GetPhysicsScene());
                    foreach (float side in new[] { 0f, -.05f, .05f })
                    {
                        var target = anchor.position + cabinet.right * side + Vector3.up * .015f;
                        var ray = new Ray(camera, target - camera);
                        float length = Vector3.Distance(target, camera);
                        var blocker = meshes.FirstOrDefault(m => m.GetComponent<MeshRenderer>().bounds.IntersectRay(ray, out var d)
                            && d < length && Intersect(ray, m.sharedMesh, m.transform.localToWorldMatrix, out var hit)
                            && hit.distance < length - .002f);
                        if (!blocker) continue;
                        failures.Add($"{cabinet.name} blocked by {blocker.transform.parent.name}/{blocker.name}; " +
                            $"eye {cabinet.InverseTransformPoint(camera):F2}, item {cabinet.InverseTransformPoint(target):F2}");
                        break;
                    }
                    box.SetOpenImmediate(false);
                    Assert.IsTrue(before.All(pair => pair.Value == pair.Key.localToWorldMatrix), cabinet.name);
                }
                Assert.AreEqual(45, boxes.Length);
                Assert.That(failures, Is.Empty, string.Join("\n", failures));
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(house);
            }
        }
    }
}
