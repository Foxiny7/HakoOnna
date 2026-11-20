using HakoOnna.Multiplayer;
using NUnit.Framework;
using Unity.Netcode;
using UnityEditor;
using UnityEngine;

namespace HakoOnna.Tests
{
    public sealed class HiderPresentationTests
    {
        [Test]
        public void PrivateHiderHasExplorerDimensionsAndRedBody()
        {
            var prefab = Resources.Load<GameObject>("HakoOnnaPlayer");
            var config = AssetDatabase.LoadAssetAtPath<GameConfig>("Assets/_Game/Config/GameConfig.asset");
            Assert.That(prefab.GetComponent<NetworkHider>(), Is.Not.Null);
            Assert.That(prefab.GetComponent<NetworkObject>().SynchronizeTransform, Is.False);
            var body = prefab.transform.Find("Body");
            Assert.That(body.localScale, Is.EqualTo(new Vector3(config.PlayerRadius * 2,
                config.PlayerHeight / 2, config.PlayerRadius * 2)));
            var color = body.GetComponent<Renderer>().sharedMaterial.color;
            Assert.That(color.r, Is.GreaterThan(color.g * 5));
            Assert.That(color.r, Is.GreaterThan(color.b * 5));
            Assert.That(body.gameObject.activeSelf, Is.False);
        }

        [Test]
        public void AllHouseContainersReachEachOtherInLocalSteps()
        {
            var house = UnityEditor.SceneManagement.EditorSceneManager.OpenPreviewScene(LocalSession.HouseScene);
            try
            {
                var root = System.Array.Find(house.GetRootGameObjects(), obj => obj.name == "House_Blockout");
                var boxes = root.GetComponentsInChildren<HakoOnna.Art.ContainerVisual>();
                Assert.AreEqual(45, boxes.Length);
                var graph = new HiderRoutes(boxes);
                for (int start = 0; start < boxes.Length; start++)
                {
                    bool distant = false;
                    var visited = new System.Collections.Generic.HashSet<int> { start };
                    var queue = new System.Collections.Generic.Queue<int>();
                    queue.Enqueue(start);
                    while (queue.Count > 0)
                    {
                        int from = queue.Dequeue();
                        for (int j = 0; j < boxes.Length; j++)
                        {
                            if (from == start && j != start)
                            {
                                if (boxes[j].transform.parent.name.Split('_')[0] == boxes[from].transform.parent.name.Split('_')[0])
                                    Assert.IsTrue(graph.Allows(from,j));
                                distant |= !graph.Allows(from,j);
                            }
                            Assert.AreEqual(graph.Allows(from,j), graph.Allows(j,from));
                            if (graph.Allows(from,j) && visited.Add(j)) queue.Enqueue(j);
                        }
                    }
                    Assert.IsTrue(distant);
                    Assert.AreEqual(boxes.Length, visited.Count);
                }
            }
            finally { UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(house); }
        }

        [Test]
        public void RollClipIsMonoForSpatialPlayback()
        {
            var roll = Resources.Load<AudioClip>("HiderRoll");
            Assert.That(roll.channels, Is.EqualTo(1));
            Assert.That(roll.length, Is.InRange(.5f, 1.5f));
        }
    }
}
