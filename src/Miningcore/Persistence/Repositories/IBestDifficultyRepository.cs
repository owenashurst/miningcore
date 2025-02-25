using System.Data;
using Miningcore.Persistence.Model;

namespace Miningcore.Persistence.Repositories;

public interface IBestDifficultyRepository
{
    Task<double> GetBestDifficultyForPoolAsync(IDbConnection con, string poolId, CancellationToken ct);

    Task UpsertAsync(IDbConnection con, IDbTransaction tx, BestDifficulty bestDifficulty, CancellationToken ct);
}
