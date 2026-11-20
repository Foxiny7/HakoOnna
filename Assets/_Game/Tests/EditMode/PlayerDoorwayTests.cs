using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HakoOnna.Tests
{
    public sealed class PlayerDoorwayTests
    {
        private const string HousePath = "Assets/_Art/Scenes/House_Blockout.unity";
        private Scene house;

        [OneTimeSetUp]
        public void LoadHouse()
        {
            house = EditorSceneManager.OpenPreviewScene(HousePath);
        }

        [OneTimeTearDown]
        public void CloseTemporaryScenes()
        {
            EditorSceneManager.ClosePreviewScene(house);
        }

        [TestCase("Door_Outside_Genkan", true)]
        [TestCase("Door_Outside_Genkan", false)]
        [TestCase("Door_Benjo_Stall", true)]
        [TestCase("Door_Benjo_Stall", false)]
        public void CanTargetDoorFrameFromEitherSide(string doorName, bool open)
        {
            var doors = house.GetRootGameObjects().SelectMany(root =>
                root.GetComponentsInChildren<HakoOnna.Art.DoorVisual>()).ToArray();
            int entrance = System.Array.FindIndex(doors, d => d.name == doorName);
            Assert.That(entrance, Is.GreaterThanOrEqualTo(0));
            var door = doors[entrance];
            try
            {
                door.SetOpenImmediate(open);
                Physics.SyncTransforms();
                var frame = door.WorldOpening;
                var normal = frame.size.x > frame.size.z ? Vector3.forward : Vector3.right;
                foreach (int side in new[] { -1, 1 })
                {
                    var ray = new Ray(frame.center + normal * side, -normal * side);
                    house.GetPhysicsScene().Raycast(ray.origin, ray.direction, out var hit, 2f,
                        Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
                    Assert.That(HakoOnna.Multiplayer.NetworkPlayer.FindDoorAtFrame(doors, ray, 2f, hit),
                        Is.EqualTo(entrance), $"side {side}, open {open}");
                }
            }
            finally
            {
                door.SetOpenImmediate(door.StartsOpen);
                Physics.SyncTransforms();
            }
        }

        [TestCase(5.46f, 0f, -1.3999f, 5.46f, 0f, 0.9899f, TestName = "Door_0")]
        [TestCase(5.46f, 0f, 9.0201f, 5.46f, 0f, 11f, TestName = "Door_1")]
        [TestCase(9.1054f, 0f, 6.37f, 12.8018f, 0.55f, 6.37f, TestName = "Door_2")]
        [TestCase(9.1054f, 0f, 13.65f, 12.8018f, 0.55f, 13.65f, TestName = "Door_4")]
        [TestCase(3.64f, 0f, 19.03f, 3.64f, 0f, 21.42f, TestName = "Door_6")]
        [TestCase(24.49f, 0.55f, 6.37f, 26.4699f, 0.55f, 6.37f, TestName = "Door_9")]
        [TestCase(14.56f, 0.55f, 1.3062f, 14.56f, 0.55f, 3.72f, TestName = "Door_15")]
        [TestCase(21.84f, 0.55f, 1.3062f, 21.84f, 0.55f, 3.72f, TestName = "Door_16")]
        [TestCase(30f, 0.55f, 1.3062f, 30f, 0.55f, 3.72f, TestName = "Door_17")]
        [TestCase(33.59f, 0.55f, 13.65f, 36.0039f, 0.55f, 13.65f, TestName = "Door_19")]
        [TestCase(14.56f, 0.55f, 16.3f, 14.56f, 0.55f, 18.7139f, TestName = "Door_20")]
        [TestCase(21.84f, 0.55f, 16.3f, 21.84f, 0.55f, 18.7139f, TestName = "Door_21")]
        [TestCase(30f, 0.55f, 16.3f, 30f, 0.55f, 18.7139f, TestName = "Door_22")]
        [TestCase(33.59f, 0.55f, 18.66f, 36.0039f, 0.55f, 18.66f, TestName = "Door_23")]
        [TestCase(33.59f, 0.55f, 1.365f, 36.0039f, 0.55f, 1.365f, TestName = "Door_24")]
        [TestCase(12.7399f, 0.55f, 18.596f, 12.7399f, 0.55f, 21.0099f, TestName = "Door_25")]
        [TestCase(19.9999f, 0.55f, 18.596f, 19.9999f, 0.55f, 21.0099f, TestName = "Door_26")]
        [TestCase(16.38f, 0f, 32.34f, 16.38f, 0.3f, 36.4176f, TestName = "Door_33")]
        [TestCase(20.0253f, 0.3f, 39.2f, 24.08f, 0f, 39.2f, TestName = "Door_34")]
        [TestCase(5.46f, 0f, -12.3201f, 5.46f, 0f, -9.5201f, TestName = "Door_35")]
        [TestCase(12.7399f, 4.15f, 16.3f, 12.7399f, 4.15f, 18.7139f, TestName = "Door_36")]
        [TestCase(23.6f, 4.15f, 16.3f, 23.6f, 4.15f, 18.7139f, TestName = "Door_37")]
        [TestCase(17.2101f, 4.15f, 13.65f, 19.19f, 4.15f, 13.65f, TestName = "Door_38")]
        [TestCase(13.6001f, 4.15f, 18.596f, 13.6001f, 4.15f, 21.0099f, TestName = "Door_39")]
        [TestCase(20.9999f, 4.15f, 18.596f, 20.9999f, 4.15f, 21.0099f, TestName = "Door_40")]
        [TestCase(13.6001f, 4.15f, 25.4001f, 13.6001f, 4.15f, 27.8139f, TestName = "Door_42")]
        [TestCase(20.9999f, 4.15f, 25.4001f, 20.9999f, 4.15f, 27.8139f, TestName = "Door_43")]
        [TestCase(9.08f, 0f, 18.4485f, 9.08f, 4.15f, 27.8308f, TestName = "KitchenStair_FromDoma")]
        [TestCase(27.3f, 0.55f, 18.5624f, 27.3f, 4.15f, 27.8308f, TestName = "MainStair_FromHall")]
        [TestCase(6.0713f, 0f, 6.37f, 13.8567f, 0.55f, 6.37f, TestName = "Entry_ToTsugi")]
        [TestCase(6.0713f, 0f, 13.65f, 13.8567f, 0.55f, 13.65f, TestName = "Kitchen_ToIma")]
        [TestCase(13.6047f, 4.15f, 20.7137f, 13.6047f, 4.15f, 25.7795f, TestName = "WalkBetweenStorageRoomDoors")]
        [TestCase(2.5609f, 0f, 12.5469f, 2.5609f, 0f, 19.0137f, TestName = "KitchenTableWestAisle")]
        [TestCase(8.7248f, 0f, 12.5469f, 8.7248f, 0f, 19.0137f, TestName = "KitchenTableEastAisle")]
        [TestCase(21.0011f, 4.15f, 20.7137f, 21.0011f, 4.15f, 25.7795f, TestName = "PassChildBedAndDesk")]
        public void CanWalkThroughDoor(float x, float y, float z, float endX, float endY, float endZ)
        {
            var gate = house.GetRootGameObjects()[0].transform.Find("Gameplay/Gate_Main");
            gate.gameObject.SetActive(false);
            try { WalkRoute(new Vector3(x,y,z), new Vector3(endX,endY,endZ)); }
            finally { gate.gameObject.SetActive(true); }
        }

        [TestCase(9.1054f, 0f, 1.36f, 12.6895f, 0.55f, 1.36f, TestName = "Sealed_3")]
        [TestCase(9.1054f, 0f, 18.66f, 12.6895f, 0.55f, 18.66f, TestName = "Sealed_5")]
        [TestCase(17.2101f, 0.55f, 6.37f, 19.19f, 0.55f, 6.37f, TestName = "Sealed_8")]
        [TestCase(17.2101f, 0.55f, 13.65f, 19.19f, 0.55f, 13.65f, TestName = "Sealed_10")]
        [TestCase(24.49f, 0.55f, 13.65f, 26.4699f, 0.55f, 13.65f, TestName = "Sealed_11")]
        [TestCase(14.56f, 0.55f, 9.0201f, 14.56f, 0.55f, 11f, TestName = "Sealed_12")]
        [TestCase(19.9999f, 0.55f, 9.0201f, 19.9999f, 0.55f, 11f, TestName = "Sealed_13")]
        [TestCase(32.4001f, 0.55f, 9.0201f, 32.4001f, 0.55f, 11f, TestName = "Sealed_14")]
        [TestCase(33.59f, 0.55f, 6.37f, 36.0039f, 0.55f, 6.37f, TestName = "Sealed_18")]
        [TestCase(13.447f, 0.55f, 24.6f, 15.5499f, 0.55f, 24.6f, TestName = "Sealed_28")]
        [TestCase(15.3901f, 4.15f, 23.2f, 17.37f, 4.15f, 23.2f, TestName = "Sealed_41")]
        public void SealedOpeningBlocksPassage(float x, float y, float z, float endX, float endY, float endZ)
        {
            WalkRoute(new Vector3(x,y,z), new Vector3(endX,endY,endZ), true);
        }

        [TestCase(18f, 1.4238f, 18f, -2.24f)]
        [TestCase(30f, 2.3835f, 30f, -2.24f)]
        [TestCase(34.9264f, 9f, 39.55f, 9f)]
        [TestCase(35.94f, 18.2447f, 35.94f, 22.26f)]
        public void ClosedEngawaGlassBlocksGardenExit(float x, float z, float endX, float endZ)
        {
            WalkRoute(new Vector3(x, .55f, z), new Vector3(endX, 0, endZ), true, "Window_Glass");
        }

        [TestCase("GardenFence_Front", 9.42f, -5f, 12.42f, -5f)]
        [TestCase("GardenFence_Front", 12.42f, -5f, 9.42f, -5f)]
        [TestCase("GardenFence_West", -4.5f, -1.5f, -4.5f, 1.5f)]
        [TestCase("GardenFence_West", -4.5f, 1.5f, -4.5f, -1.5f)]
        [TestCase("GardenFence_East", 42f, 18.52f, 42f, 21.52f)]
        [TestCase("GardenFence_East", 42f, 21.52f, 42f, 18.52f)]
        public void BambooFenceBlocksCrossing(string fence, float x, float z, float endX, float endZ)
        {
            WalkRoute(new Vector3(x, 0, z), new Vector3(endX, 0, endZ), true, fence);
        }

        [TestCase(27.3f, .55f)]
        [TestCase(9.08f, 0f)]
        public void StairSideBlocksEntry(float x, float y)
        {
            WalkRoute(new Vector3(x - 2.2f,y,23.205f), new Vector3(x,y,23.205f), true);
        }

        [Test]
        public void SpawnPointsAreCovered()
        {
            var spawns = house.GetRootGameObjects()[0].transform.Find("Gameplay/ExplorerSpawnPoints");
            Assert.AreEqual(5, spawns.childCount);
            foreach (Transform spawn in spawns)
            {
                Assert.IsTrue(house.GetPhysicsScene().Raycast(spawn.position + Vector3.up * 2, Vector3.up, out var hit, 3f));
                Assert.AreEqual("Slab_1F",hit.collider.name);
            }
        }

        [Test]
        public void EveryContainerCanBeTargetedFromItsInteractionPoint()
        {
            var containers = house.GetRootGameObjects().Single(go => go.name == "House_Blockout")
                .transform.Find("Containers");
            Assert.Greater(containers.childCount, 0);
            var config = AssetDatabase.LoadAssetAtPath<GameConfig>("Assets/_Game/Config/GameConfig.asset");
            foreach (Transform box in containers)
            {
                var eye = box.Find("InteractPoint").position + Vector3.up * config.EyeHeight;
                var collision = box.Find("Visual/Collision").GetComponent<BoxCollider>();
                var direction = (collision.bounds.center - eye).normalized;
                Assert.IsTrue(house.GetPhysicsScene().Raycast(eye, direction, out var hit, config.SearchReach,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore), box.name);
                Assert.AreEqual(collision, hit.collider, box.name);
            }
        }

        [Test]
        public void ContainerFootprintsClearWallsAndEachOther()
        {
            var root = house.GetRootGameObjects()[0].transform;
            var containers = root.Find("Containers");
            Assert.AreEqual(45, containers.childCount);
            var occupied = new System.Collections.Generic.List<Bounds>();
            var walls = root.Find("Structure").GetComponentsInChildren<BoxCollider>();
            foreach (Transform item in containers)
            {
                var box = item.GetComponentInChildren<BoxCollider>().bounds;
                box.Expand(-.005f);
                foreach (var other in occupied) Assert.IsFalse(box.Intersects(other), item.name);
                foreach (var wall in walls)
                    if (wall.name.StartsWith("Wall")) Assert.IsFalse(box.Intersects(wall.bounds), item.name);
                occupied.Add(box);
            }
        }

        [Test]
        public void RoomsHaveUsableExitAndClearDoorways()
        {
            var root = house.GetRootGameObjects()[0].transform;
            var furniture = root.Find("Containers").GetComponentsInChildren<BoxCollider>()
                .Concat(root.Find("Props").GetComponentsInChildren<BoxCollider>()).ToArray();
            foreach (Transform room in root.Find("Structure"))
            {
                var floor = room.Find("Floor");
                if (floor == null || room.name.Contains("Kaidan") || room.name.Contains("Benjo")) continue;
                var area = floor.GetComponent<Renderer>().bounds;
                var walls = room.GetComponentsInChildren<BoxCollider>();
                int exits = 0;
                for (int edge = 0; edge < 4; edge++)
                {
                    bool alongX = edge < 2;
                    float low = alongX ? area.min.x : area.min.z;
                    float high = alongX ? area.max.x : area.max.z;
                    float line = edge == 0 ? area.min.z + .04f : edge == 1 ? area.max.z - .04f :
                        edge == 2 ? area.min.x + .04f : area.max.x - .04f;
                    int clear = 0;
                    for (float pos = low + .10f; pos < high - .08f; pos += .04f)
                    {
                        var point = alongX ? new Vector3(pos, area.max.y + 1, line) : new Vector3(line, area.max.y + 1, pos);
                        bool solid = System.Array.Exists(walls, w => w.bounds.Contains(point));
                        if (solid) { clear = 0; continue; }
                        clear++;
                        if (clear == 15) exits++;
                        point.y = area.max.y + .2f;
                        var space = new Bounds(point, alongX ? new Vector3(.035f,.1f,.6f) : new Vector3(.6f,.1f,.035f));
                        foreach (var box in furniture) Assert.IsFalse(space.Intersects(box.bounds), room.name + " / " + box.transform.root.name);
                    }
                }
                Assert.GreaterOrEqual(exits, 1, room.name);
            }
        }

        [Test]
        public void FurnitureLeavesInteractionPointsAndOtherFurnitureClear()
        {
            var root = house.GetRootGameObjects()[0].transform;
            var furniture = root.Find("Containers").GetComponentsInChildren<BoxCollider>()
                .Concat(root.Find("Props").GetComponentsInChildren<BoxCollider>()).ToArray();
            var walls = root.Find("Structure").GetComponentsInChildren<BoxCollider>();
            for (int i = 0; i < furniture.Length; i++)
            {
                var bounds = furniture[i].bounds;
                bounds.Expand(-.008f);
                for (int j = 0; j < i; j++)
                    Assert.IsFalse(bounds.Intersects(furniture[j].bounds), furniture[i].name + " / " + furniture[j].name);
                foreach (var wall in walls)
                    if (wall.name.StartsWith("Wall")) Assert.IsFalse(bounds.Intersects(wall.bounds), furniture[i].name);
            }

            foreach (var marker in root.Find("Gameplay").GetComponentsInChildren<Transform>())
            {
                if (marker.name != "InteractPoint" && marker.name != "HatchAnchor" && !marker.name.StartsWith("SpawnPoint_")) continue;
                var standing = new Bounds(marker.position + Vector3.up * .83f, new Vector3(.48f,1.64f,.48f));
                foreach (var item in furniture) Assert.IsFalse(standing.Intersects(item.bounds), marker.parent.name + " / " + item.name);
            }
        }

        private void WalkRoute(Vector3 start, Vector3 end, bool blocked = false, string barrier = null)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Prefabs/Explorer.prefab");
            var player = Object.Instantiate(prefab);
            SceneManager.MoveGameObjectToScene(player, house);
            player.GetComponent<HakoOnna.Multiplayer.NetworkPlayer>().ConfigureDimensions();
            var controller = player.GetComponent<CharacterController>();
            controller.enabled = false;
            player.transform.position = start + Vector3.up * 0.04f;
            controller.enabled = true;
            Physics.SyncTransforms();
            try
            {
                var target = new Vector3(end.x, 0, end.z);
                for (var step = 0; step < 400; step++)
                {
                    var delta = target - new Vector3(player.transform.position.x, 0, player.transform.position.z);
                    if (delta.magnitude < 0.04f)
                        break;
                    controller.Move(Vector3.ClampMagnitude(delta, 0.04f));
                    controller.Move(Vector3.down * 0.06f);
                }

                var actual = new Vector2(player.transform.position.x, player.transform.position.z);
                if (blocked)
                {
                    var origin = new Vector2(start.x, start.z);
                    var route = new Vector2(end.x, end.z) - origin;
                    float progress = Vector2.Dot(actual - origin, route.normalized);
                    Assert.Less(progress, route.magnitude - .55f);
                    if (barrier != null)
                    {
                        controller.enabled = false;
                        var direction = new Vector3(route.x, 0, route.y).normalized;
                        Assert.IsTrue(house.GetPhysicsScene().Raycast(start + Vector3.up, direction,
                            out var hit, route.magnitude), barrier);
                        Assert.AreEqual(barrier, hit.collider.name);
                        Assert.That(hit.distance - progress, Is.EqualTo(controller.radius).Within(.10f),
                            "Player should stop against the near face of " + barrier);
                    }
                    Assert.That(player.transform.position.y, Is.EqualTo(start.y).Within(.10f));
                    return;
                }
                Assert.That(Vector2.Distance(actual, new Vector2(end.x, end.z)), Is.LessThan(0.08f),
                    "Doorway blocked the controller at " + player.transform.position);
                Assert.That(player.transform.position.y, Is.InRange(end.y - 0.04f, end.y + 0.1f));
            }
            finally
            {
                Object.DestroyImmediate(player);
            }
        }
    }
}
