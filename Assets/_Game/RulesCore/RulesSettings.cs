using System;

namespace HakoOnna.Rules
{
    public sealed class RulesSettings
    {
        public RulesSettings(double searchSeconds, double transferCooldownSeconds)
        {
            if (!(searchSeconds > 0) || double.IsInfinity(searchSeconds * 2))
                throw new ArgumentOutOfRangeException(nameof(searchSeconds));
            if (!(transferCooldownSeconds >= 0) || double.IsInfinity(transferCooldownSeconds))
                throw new ArgumentOutOfRangeException(nameof(transferCooldownSeconds));

            SearchSeconds = searchSeconds;
            TransferCooldownSeconds = transferCooldownSeconds;
        }

        public double SearchSeconds { get; }
        public double TransferCooldownSeconds { get; }
    }
}
