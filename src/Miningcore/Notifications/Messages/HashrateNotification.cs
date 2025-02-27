using JetBrains.Annotations;

namespace Miningcore.Notifications.Messages;

public record HashrateNotification
{
    public string PoolId { get; set; }

    public double Hashrate { get; set; }

    [CanBeNull]
    public string Miner { get; set; }

    [CanBeNull]
    public string Worker { get; set; }
}
