using System;
using System.Collections.Generic;
using System.Linq;

namespace HakoOnna.Rules
{
    public enum Role { Explorer, HakoOnna, HakoBito, Spectator }
    public enum SearchKind { Normal, Deep }
    public enum ItemKind { Gasoline, WhiteWoodenStake, RustyChain, Diary, GateKey, MaryDoll, Remains, Pliers, Flashlight }
    public enum WeaknessOutcome { Rejected, Consumed, HakoOnnaDefeated, ExplorerConverted }

    // Server-only state. Dispatch all commands and clock updates serially.
    public sealed class MatchRules
    {
        private readonly RulesSettings settings;
        private readonly ItemKind weakness;
        private readonly Dictionary<int, Player> players = new Dictionary<int, Player>();
        private readonly Dictionary<int, Container> containers = new Dictionary<int, Container>();
        private Player hakoOnna;
        private double now;

        public MatchRules(RulesSettings settings, ItemKind weakness)
        {
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
            if (!IsWeakness(weakness))
                throw new ArgumentOutOfRangeException(nameof(weakness));
            this.weakness = weakness;
        }

        public const int InventorySize = 9;

        public Player AddExplorer(int playerId, params ItemKind?[] items)
        {
            if (items.Length >= InventorySize)
                throw new ArgumentException("Leave room for the starting flashlight.", nameof(items));
            var slots = new ItemKind?[InventorySize];
            items.CopyTo(slots, 0);
            slots[items.Length] = ItemKind.Flashlight;
            var player = new Player(Role.Explorer, slots);
            players.Add(playerId, player);
            return player;
        }

        public Player PlaceHakoOnna(int playerId, int containerId)
        {
            if (hakoOnna != null || containers[containerId].SearcherId.HasValue)
                throw new InvalidOperationException("Invalid initial Hako Onna placement.");

            var player = new Player(Role.HakoOnna, Array.Empty<ItemKind?>()) { ContainerId = containerId };
            players.Add(playerId, player);
            hakoOnna = player;
            return player;
        }

        public Container AddContainer(int containerId, ItemKind? item = null)
        {
            var container = new Container(item);
            containers.Add(containerId, container);
            return container;
        }

        public bool BeginSearch(int playerId, int containerId, SearchKind kind)
        {
            if (!ReserveSearch(playerId, containerId, kind)) return false;
            StartSearchClock(playerId, containerId);
            return true;
        }

        public bool ReserveSearch(int playerId, int containerId, SearchKind kind)
        {
            if (!players.TryGetValue(playerId, out var player) || player.Role != Role.Explorer ||
                player.SearchContainerId.HasValue || !containers.TryGetValue(containerId, out var container) ||
                container.SearcherId.HasValue || (kind != SearchKind.Normal && kind != SearchKind.Deep))
                return false;

            player.SearchContainerId = containerId;
            container.SearcherId = playerId;
            container.Kind = kind;
            container.SearchEndsAt = double.PositiveInfinity;
            return true;
        }

        public void StartSearchClock(int playerId, int containerId)
        {
            var container = containers[containerId];
            if (container.SearcherId != playerId || !double.IsPositiveInfinity(container.SearchEndsAt)) return;
            container.SearchStartedAt = now;
            container.SearchEndsAt = now + settings.SearchSeconds * (container.Kind == SearchKind.Deep ? 2 : 1);
        }

        public bool EndSearch(int playerId, int containerId)
        {
            if (!players.TryGetValue(playerId, out var player) || player.SearchContainerId != containerId)
                return false;

            CloseSearch(player, containers[containerId]);
            return true;
        }

        public ItemKind? ContainerItem(int containerId) => containers[containerId].Item;

        public bool ExchangeWithContainer(int playerId, int containerId, int slot, bool take)
        {
            if (!players.TryGetValue(playerId, out var player) || player.SearchContainerId != containerId ||
                !containers.TryGetValue(containerId, out var box) || !box.IsOpen ||
                slot < 0 || slot >= player.Items.Length)
                return false;

            if (take ? !box.Item.HasValue || player.Items[slot].HasValue :
                box.Item.HasValue || !player.Items[slot].HasValue)
                return false;

            (player.Items[slot], box.Item) = (box.Item, player.Items[slot]);
            return true;
        }

        public void RemoveExplorer(int playerId)
        {
            if (!players.TryGetValue(playerId, out var player) || player.Role != Role.Explorer)
                return;
            if (player.SearchContainerId.HasValue)
                EndSearch(playerId, player.SearchContainerId.Value);
            players.Remove(playerId);
        }

        public double SearchProgress(int containerId)
        {
            var container = containers[containerId];
            if (container.IsOpen)
                return 1;
            if (!container.SearcherId.HasValue)
                return 0;

            return (now - container.SearchStartedAt) / (container.SearchEndsAt - container.SearchStartedAt);
        }

        public void AdvanceTo(double serverSeconds)
        {
            if (!(serverSeconds >= now) || double.IsInfinity(serverSeconds))
                throw new ArgumentOutOfRangeException(nameof(serverSeconds));

            now = serverSeconds;
            foreach (var pair in containers)
            {
                var container = pair.Value;
                if (!container.SearcherId.HasValue || container.IsOpen || container.SearchEndsAt > now)
                    continue;

                var explorer = players[container.SearcherId.Value];
                if (players.Values.Any(player => player.ContainerId == pair.Key))
                    ConvertExplorer(explorer, pair.Key);
                else
                    container.IsOpen = true;
            }
        }

        public bool TryTransfer(int playerId, int destinationId)
        {
            if (!players.TryGetValue(playerId, out var player) || !player.ContainerId.HasValue ||
                player.ContainerId == destinationId || now < player.NextTransferAt ||
                !containers.TryGetValue(destinationId, out var destination) || destination.SearcherId.HasValue)
                return false;

            player.ContainerId = destinationId;
            player.NextTransferAt = now + settings.TransferCooldownSeconds;
            return true;
        }

        public WeaknessOutcome UseWeakness(int explorerId, int itemIndex, int containerId)
        {
            if (!players.TryGetValue(explorerId, out var explorer) || explorer.Role != Role.Explorer ||
                itemIndex < 0 || itemIndex >= explorer.Inventory.Count || !containers.ContainsKey(containerId))
                return WeaknessOutcome.Rejected;

            var item = explorer.Inventory[itemIndex];
            if (!IsWeakness(item))
                return WeaknessOutcome.Rejected;

            explorer.Items[itemIndex] = null;
            foreach (var player in players.Values)
            {
                if (player.Role == Role.HakoBito && player.ContainerId == containerId)
                {
                    player.Role = Role.Spectator;
                    player.ContainerId = null;
                }
            }

            if (hakoOnna?.ContainerId != containerId)
                return WeaknessOutcome.Consumed;

            if (item == weakness)
            {
                hakoOnna.ContainerId = null;
                return WeaknessOutcome.HakoOnnaDefeated;
            }

            // The newly converted explorer was not a target of this use.
            ConvertExplorer(explorer, containerId);
            return WeaknessOutcome.ExplorerConverted;
        }

        private void ConvertExplorer(Player explorer, int containerId)
        {
            if (explorer.SearchContainerId.HasValue)
                CloseSearch(explorer, containers[explorer.SearchContainerId.Value]);

            Array.Clear(explorer.Items, 0, explorer.Items.Length);
            explorer.Role = Role.HakoBito;
            explorer.ContainerId = containerId;
        }

        private static void CloseSearch(Player explorer, Container container)
        {
            explorer.SearchContainerId = null;
            container.SearcherId = null;
            container.IsOpen = false;
        }

        private static bool IsWeakness(ItemKind? item)
        {
            return item == ItemKind.Gasoline || item == ItemKind.WhiteWoodenStake || item == ItemKind.RustyChain;
        }

        public sealed class Player
        {
            internal Player(Role role, ItemKind?[] items)
            {
                Role = role;
                Items = (ItemKind?[])items.Clone();
                Inventory = Array.AsReadOnly(Items);
            }

            public Role Role { get; internal set; }
            public int? ContainerId { get; internal set; }
            public IReadOnlyList<ItemKind?> Inventory { get; }

            internal ItemKind?[] Items { get; }
            public int PickupSlot(int selected) => Inventory[selected].HasValue ? Array.IndexOf(Items, null) : selected;
            internal int? SearchContainerId { get; set; }
            internal double NextTransferAt { get; set; }
        }

        public sealed class Container
        {
            internal Container(ItemKind? item)
            {
                Item = item;
            }

            public ItemKind? Item { get; internal set; }
            public int? SearcherId { get; internal set; }
            public bool IsOpen { get; internal set; }

            internal double SearchStartedAt { get; set; }
            internal SearchKind Kind { get; set; }
            internal double SearchEndsAt { get; set; }
        }
    }
}
