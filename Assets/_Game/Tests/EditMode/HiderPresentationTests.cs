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
        public void RollClipIsMonoForSpatialPlayback()
        {
            var roll = Resources.Load<AudioClip>("HiderRoll");
            Assert.That(roll.channels, Is.EqualTo(1));
            Assert.That(roll.length, Is.InRange(.5f, 1.5f));
        }
    }
}
