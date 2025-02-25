namespace Miningcore.Persistence.Model;

public class BestDifficulty
{
    public string PoolId { get; set; }
    public string Miner { get; set; }
    public string Worker { get; set; }
    public double Difficulty { get; set; }
    public DateTime Updated { get; set; }
}
