using System;
using System.Collections.Generic;
using System.Linq;

namespace HakoOnna.Rules
{
    public enum Role { Explorer, HakoOnna, HakoBito, Spectator }
    public enum SearchKind { Normal, Deep }
    public enum ItemKind { WeaknessA, WeaknessB, WeaknessC, Diary, GateKey, MaryDoll, Remains, Pliers }
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

        public Player AddExplorer(int playerId, params ItemKind?[] items)
        {
            var player = new Player(Role.Explorer, items);
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
            if (!players.TryGetValue(playerId, out var player) || player.Role != Role.Explorer ||
                player.SearchContainerId.HasValue || !containers.TryGetValue(containerId, out var container) ||
                container.SearcherId.HasValue || (kind != SearchKind.Normal && kind != SearchKind.Deep))
                return false;

            player.SearchContainerId = containerId;
            container.SearcherId = playerId;
            container.SearchStartedAt = now;
            container.SearchEndsAt = now + settings.SearchSeconds * (kind == SearchKind.Deep ? 2 : 1);
            return true;
        }

        public bool EndSearch(int playerId, int containerId)
        {
            if (!players.TryGetValue(playerId, out var player) || player.SearchContainerId != containerId)
                return false;

            CloseSearch(player, containers[containerId]);
            return true;
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
            return item == ItemKind.WeaknessA || item == ItemKind.WeaknessB || item == ItemKind.WeaknessC;
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
            internal int? SearchContainerId { get; set; }
            internal double NextTransferAt { get; set; }
        }

        public sealed class Container
        {
            internal Container(ItemKind? item)
            {
                Item = item;
            }

            public ItemKind? Item { get; }
            public int? SearcherId { get; internal set; }
            public bool IsOpen { get; internal set; }

            internal double SearchStartedAt { get; set; }
            internal double SearchEndsAt { get; set; }
        }
    }
}
