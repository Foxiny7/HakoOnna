using HakoOnna.Rules;
using NUnit.Framework;

namespace HakoOnna.Tests
{
    public sealed class MatchRulesTests
    {
        private const int HakoOnnaId = 1;
        private const int ExplorerId = 2;
        private const int Source = 10;
        private const int Destination = 20;

        private MatchRules rules;
        private MatchRules.Player hakoOnna;
        private MatchRules.Player explorer;
        private MatchRules.Container source;
        private MatchRules.Container destination;

        [SetUp]
        public void SetUp()
        {
            rules = new MatchRules(new RulesSettings(2, 3), ItemKind.Gasoline);
            source = rules.AddContainer(Source, ItemKind.Diary);
            destination = rules.AddContainer(Destination);
            hakoOnna = rules.PlaceHakoOnna(HakoOnnaId, Source);
            explorer = rules.AddExplorer(ExplorerId, ItemKind.Gasoline, ItemKind.WhiteWoodenStake, ItemKind.GateKey);
        }

        [Test]
        public void ExplorerStartsWithAFlashlightInAnInventorySlot()
        {
            var startingExplorer = rules.AddExplorer(3);

            Assert.AreEqual(1, startingExplorer.Inventory.Count);
            Assert.AreEqual(ItemKind.Flashlight, startingExplorer.Inventory[0]);
        }

        [Test]
        public void TransferBeforeSearchCompletesLetsExplorerOpenContainer()
        {
            Assert.IsTrue(rules.BeginSearch(ExplorerId, Source, SearchKind.Normal));
            rules.AdvanceTo(1);
            Assert.IsFalse(source.IsOpen);
            Assert.AreEqual(0.5, rules.SearchProgress(Source));
            Assert.IsTrue(rules.TryTransfer(HakoOnnaId, Destination));
            rules.AdvanceTo(2);

            Assert.AreEqual(Role.Explorer, explorer.Role);
            Assert.IsTrue(source.IsOpen);
            Assert.AreEqual(1d, rules.SearchProgress(Source));
            Assert.AreEqual(ItemKind.Diary, source.Item);
            Assert.IsTrue(rules.EndSearch(ExplorerId, Source));
            Assert.IsFalse(source.IsOpen);
            Assert.AreEqual(0d, rules.SearchProgress(Source));
        }

        [Test]
        public void SearchBeforeTransferConvertsExplorerAndKeepsContainerItem()
        {
            Assert.IsTrue(rules.BeginSearch(ExplorerId, Source, SearchKind.Normal));
            rules.AdvanceTo(2);
            Assert.IsTrue(rules.TryTransfer(HakoOnnaId, Destination));

            Assert.AreEqual(Role.HakoBito, explorer.Role);
            Assert.AreEqual(Source, explorer.ContainerId);
            Assert.IsNull(explorer.Inventory[0]);
            Assert.IsNull(explorer.Inventory[1]);
            Assert.IsNull(explorer.Inventory[2]);
            Assert.IsNull(explorer.Inventory[3]);
            Assert.IsFalse(source.IsOpen);
            Assert.IsNull(source.SearcherId);
            Assert.AreEqual(ItemKind.Diary, source.Item);
        }

        [Test]
        public void CancelResetsProgressAndReleasesContainer()
        {
            rules.AddExplorer(3);
            Assert.IsTrue(rules.BeginSearch(ExplorerId, Source, SearchKind.Normal));
            rules.AdvanceTo(1);
            Assert.AreEqual(0.5, rules.SearchProgress(Source));
            Assert.IsFalse(rules.EndSearch(3, Source));
            Assert.IsTrue(rules.EndSearch(ExplorerId, Source));
            rules.AdvanceTo(2);

            Assert.AreEqual(Role.Explorer, explorer.Role);
            Assert.AreEqual(0d, rules.SearchProgress(Source));
            Assert.IsTrue(rules.BeginSearch(3, Source, SearchKind.Normal));
            rules.AdvanceTo(3);
            Assert.AreEqual(0.5, rules.SearchProgress(Source));
        }

        [Test]
        public void SearchLocksContainerDuringProgressAndWhileOpen()
        {
            rules.AddExplorer(3);
            Assert.IsTrue(rules.BeginSearch(ExplorerId, Destination, SearchKind.Normal));
            Assert.IsFalse(rules.BeginSearch(3, Destination, SearchKind.Deep));
            Assert.IsFalse(rules.BeginSearch(ExplorerId, Source, SearchKind.Normal));
            Assert.IsFalse(rules.TryTransfer(HakoOnnaId, Destination));
            rules.AdvanceTo(2);
            Assert.IsTrue(destination.IsOpen);
            Assert.IsFalse(rules.BeginSearch(3, Destination, SearchKind.Normal));
            Assert.IsFalse(rules.TryTransfer(HakoOnnaId, Destination));

            Assert.IsTrue(rules.EndSearch(ExplorerId, Destination));
            Assert.IsTrue(rules.TryTransfer(HakoOnnaId, Destination));
        }

        [Test]
        public void DeepSearchUsesTwiceTheDurationAndStillConvertsInAnOccupiedContainer()
        {
            Assert.IsTrue(rules.BeginSearch(ExplorerId, Source, SearchKind.Deep));
            rules.AdvanceTo(2);
            Assert.AreEqual(0.5, rules.SearchProgress(Source));
            Assert.IsFalse(source.IsOpen);
            Assert.AreEqual(Role.Explorer, explorer.Role);
            rules.AdvanceTo(4);

            Assert.AreEqual(Role.HakoBito, explorer.Role);
            Assert.IsFalse(source.IsOpen);
            Assert.IsNull(source.SearcherId);
        }

        [Test]
        public void HakoOnnaAndHakoBitoUseIndependentTransferCooldowns()
        {
            ConvertInSource(3, 2);
            Assert.IsTrue(rules.TryTransfer(HakoOnnaId, Destination));
            Assert.IsTrue(rules.TryTransfer(3, Destination));
            Assert.IsFalse(rules.TryTransfer(ExplorerId, Destination));
            rules.AdvanceTo(4.99);
            Assert.IsFalse(rules.TryTransfer(HakoOnnaId, Source));
            Assert.IsFalse(rules.TryTransfer(3, Source));
            rules.AdvanceTo(5);
            Assert.IsTrue(rules.TryTransfer(HakoOnnaId, Source));
            Assert.IsTrue(rules.TryTransfer(3, Source));
        }

        [Test]
        public void HakoBitoAloneCanConvertExplorer()
        {
            ConvertInSource(3, 2);
            Assert.IsTrue(rules.TryTransfer(HakoOnnaId, Destination));
            Assert.IsTrue(rules.BeginSearch(ExplorerId, Source, SearchKind.Normal));
            rules.AdvanceTo(4);

            Assert.AreEqual(Role.HakoBito, explorer.Role);
            Assert.AreEqual(Source, explorer.ContainerId);
        }

        [Test]
        public void CorrectWeaknessDefeatsHakoOnnaAndAllExistingHakoBitoInContainer()
        {
            var first = ConvertInSource(3, 2);
            var second = ConvertInSource(4, 4);
            var elsewhere = ConvertInSource(5, 6);
            Assert.IsTrue(rules.TryTransfer(5, Destination));

            Assert.AreEqual(WeaknessOutcome.HakoOnnaDefeated, rules.UseWeakness(ExplorerId, 0, Source));
            Assert.IsNull(hakoOnna.ContainerId);
            Assert.AreEqual(Role.Spectator, first.Role);
            Assert.AreEqual(Role.Spectator, second.Role);
            Assert.IsNull(first.ContainerId);
            Assert.IsNull(second.ContainerId);
            Assert.AreEqual(Role.HakoBito, elsewhere.Role);
            Assert.AreEqual(Destination, elsewhere.ContainerId);
            Assert.IsFalse(rules.TryTransfer(3, Destination));
            Assert.AreEqual(Role.Explorer, explorer.Role);
            Assert.IsNull(explorer.Inventory[0]);
            Assert.AreEqual(ItemKind.WhiteWoodenStake, explorer.Inventory[1]);
            Assert.AreEqual(ItemKind.Diary, source.Item);
        }

        [Test]
        public void WrongWeaknessRemovesExistingHakoBitoThenConvertsUserAndClosesTheirSearch()
        {
            var first = ConvertInSource(3, 2);
            var second = ConvertInSource(4, 4);
            Assert.IsTrue(rules.BeginSearch(ExplorerId, Destination, SearchKind.Normal));

            Assert.AreEqual(WeaknessOutcome.ExplorerConverted, rules.UseWeakness(ExplorerId, 1, Source));
            Assert.AreEqual(Role.Spectator, first.Role);
            Assert.AreEqual(Role.Spectator, second.Role);
            Assert.AreEqual(Source, hakoOnna.ContainerId);
            Assert.AreEqual(Role.HakoBito, explorer.Role);
            Assert.AreEqual(Source, explorer.ContainerId);
            Assert.IsNull(explorer.Inventory[0]);
            Assert.IsNull(explorer.Inventory[1]);
            Assert.IsNull(explorer.Inventory[2]);
            Assert.IsNull(explorer.Inventory[3]);
            Assert.IsNull(destination.SearcherId);
            Assert.AreEqual(0d, rules.SearchProgress(Destination));
        }

        [Test]
        public void AnyWeaknessDefeatsHakoBitoWithoutHakoOnna()
        {
            var hakoBito = ConvertInSource(3, 2);
            Assert.IsTrue(rules.TryTransfer(HakoOnnaId, Destination));

            Assert.AreEqual(WeaknessOutcome.Consumed, rules.UseWeakness(ExplorerId, 1, Source));
            Assert.AreEqual(Role.Spectator, hakoBito.Role);
            Assert.AreEqual(Role.Explorer, explorer.Role);
            Assert.AreEqual(Destination, hakoOnna.ContainerId);
        }

        [Test]
        public void EmptyContainerConsumesWeaknessAndReplayingTheSlotDoesNotConsumeAnotherItem()
        {
            Assert.AreEqual(WeaknessOutcome.Rejected, rules.UseWeakness(ExplorerId, 2, Destination));
            Assert.AreEqual(WeaknessOutcome.Rejected, rules.UseWeakness(ExplorerId, 0, 99));
            Assert.AreEqual(ItemKind.Gasoline, explorer.Inventory[0]);
            Assert.AreEqual(WeaknessOutcome.Consumed, rules.UseWeakness(ExplorerId, 0, Destination));
            Assert.AreEqual(WeaknessOutcome.Rejected, rules.UseWeakness(ExplorerId, 0, Destination));
            Assert.IsNull(explorer.Inventory[0]);
            Assert.AreEqual(ItemKind.WhiteWoodenStake, explorer.Inventory[1]);
            Assert.AreEqual(Role.Explorer, explorer.Role);
        }

        private MatchRules.Player ConvertInSource(int playerId, double time)
        {
            var player = rules.AddExplorer(playerId);
            Assert.IsTrue(rules.BeginSearch(playerId, Source, SearchKind.Normal));
            rules.AdvanceTo(time);
            Assert.AreEqual(Role.HakoBito, player.Role);
            return player;
        }
    }
}
