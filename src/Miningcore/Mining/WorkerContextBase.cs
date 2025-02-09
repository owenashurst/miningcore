using Miningcore.Configuration;
using Miningcore.Time;
using Miningcore.VarDiff;

namespace Miningcore.Mining;

public class ShareStats
{
    public int ValidShares { get; set; }
    public int InvalidShares { get; set; }
}

public class WorkerContextBase
{
    private int? pendingDifficulty;
    private string userAgent;

    public ShareStats Stats { get; set; }
    public VarDiffContext VarDiff { get; set; }
    public DateTime Created { get; set; }
    public DateTime LastActivity { get; set; }
    public bool IsAuthorized { get; set; }
    public bool IsSubscribed { get; set; }

    /// <summary>
    /// Difficulty assigned to this worker, either static or updated through VarDiffManager
    /// </summary>
    public int Difficulty { get; set; }

    /// <summary>
    /// Previous difficulty assigned to this worker
    /// </summary>
    public int? PreviousDifficulty { get; set; }

    /// <summary>
    /// UserAgent reported by Stratum
    /// </summary>
    public string UserAgent
    {
        get => userAgent;
        set
        {
            userAgent = value;
        }
    }

    public void Init(int difficulty, VarDiffConfig varDiffConfig, IMasterClock clock)
    {
        Difficulty = difficulty;
        LastActivity = clock.Now;
        Created = clock.Now;
        Stats = new ShareStats();

        if(varDiffConfig != null)
        {
            VarDiff = new VarDiffContext
            {
                Created = Created,
                Config = varDiffConfig
            };
        }
    }

    public void EnqueueNewDifficulty(int difficulty)
    {
        pendingDifficulty = difficulty;
    }

    public bool HasPendingDifficulty => pendingDifficulty.HasValue;

    public bool ApplyPendingDifficulty()
    {
        if(pendingDifficulty.HasValue)
        {
            SetDifficulty(pendingDifficulty.Value);
            pendingDifficulty = null;

            return true;
        }

        return false;
    }

    public void SetDifficulty(int difficulty)
    {
        PreviousDifficulty = Difficulty;
        Difficulty = difficulty;
    }

}
