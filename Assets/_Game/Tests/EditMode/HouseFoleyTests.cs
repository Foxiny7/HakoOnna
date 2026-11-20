using HakoOnna.Art;
using HakoOnna.Multiplayer;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace HakoOnna.Tests
{
    public sealed class HouseFoleyTests
    {
        [Test]
        public void EveryContainerAndWalkableMaterialHasAnImportedSound()
        {
            var scene = EditorSceneManager.OpenPreviewScene(LocalSession.HouseScene);
            try
            {
                var house = System.Array.Find(scene.GetRootGameObjects(), obj => obj.name == "House_Blockout");
                var containers = house.GetComponentsInChildren<ContainerVisual>();
                Assert.AreEqual(45, containers.Length);
                foreach (var container in containers)
                {
                    HouseFoley.Bind(container);
                    Check(container.GetComponent<AudioSource>().clip, container.transform.parent.name);
                }
                var foley = new HouseFoley(house.transform);
                foreach (FootSurface surface in System.Enum.GetValues(typeof(FootSurface)))
                    Check(foley.Step(surface), surface.ToString());
                foreach (var floor in house.GetComponentsInChildren<MeshRenderer>())
                {
                    if (floor.name != "Floor") continue;
                    var room = floor.transform.parent.name.Split('_');
                    if (room.Length < 2) continue;
                    var expected = room[1] switch
                    {
                        "Tsugi" or "Shinshitsu" => FootSurface.Tatami,
                        "Benjo" => FootSurface.Tile,
                        "Rouka2F" or "Kura" => FootSurface.Wood,
                        "Genkan" => FootSurface.Stone,
                        _ => (FootSurface?)null
                    };
                    if (expected.HasValue)
                        Assert.AreEqual(expected.Value, foley.SurfaceAt(new Vector3(floor.bounds.center.x,
                            floor.bounds.max.y, floor.bounds.center.z)), room[1]);
                }
                Assert.AreEqual(FootSurface.Gravel, foley.SurfaceAt(new Vector3(5, 0, -4)));
                Assert.AreEqual(FootSurface.Earth, foley.SurfaceAt(new Vector3(-3, 0, -3)));
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        private static void Check(AudioClip clip, string label)
        {
            Assert.IsNotNull(clip, label);
            Assert.AreEqual(1, clip.channels, label);
            Assert.Greater(clip.length, .06f, label);
        }
    }
}
