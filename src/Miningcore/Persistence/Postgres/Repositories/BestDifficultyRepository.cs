using System.Data;
using AutoMapper;
using Dapper;
using Miningcore.Persistence.Model;
using Miningcore.Persistence.Repositories;

namespace Miningcore.Persistence.Postgres.Repositories;

public class BestDifficultyRepository(IMapper mapper) : IBestDifficultyRepository
{
    public async Task<double> GetBestDifficultyForPoolAsync(IDbConnection con, string poolId, CancellationToken ct)
    {
        const string query = @"SELECT difficulty FROM bestdifficulty WHERE poolid = @poolId ORDER BY difficulty DESC LIMIT 1";

        var entity = await con.QuerySingleOrDefaultAsync<double>(new CommandDefinition(query, new {poolId}, cancellationToken: ct));

        return mapper.Map<double>(entity);
    }

    public Task UpsertAsync(IDbConnection con, IDbTransaction tx, BestDifficulty bestDifficulty, CancellationToken ct)
    {
        var mapped = mapper.Map<Entities.BestDifficulty>(bestDifficulty);

        // Only update if the new difficulty is greater than the existing one
        const string query = @"
            INSERT INTO bestdifficulty(poolid, miner, worker, difficulty, updated)
            VALUES(@poolid, @miner, @worker, @difficulty, @updated)
            ON CONFLICT (poolid, miner, worker)
            DO UPDATE SET
                difficulty = CASE
                                WHEN bestdifficulty.difficulty < EXCLUDED.difficulty
                                THEN EXCLUDED.difficulty
                                ELSE bestdifficulty.difficulty
                            END,
                updated = CASE
                             WHEN bestdifficulty.difficulty < EXCLUDED.difficulty
                             THEN EXCLUDED.updated
                             ELSE bestdifficulty.updated
                          END";

        return con.ExecuteAsync(new CommandDefinition(query, mapped, tx, cancellationToken: ct));
    }
}
