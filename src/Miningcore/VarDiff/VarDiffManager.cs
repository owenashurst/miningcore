using CircularBuffer;
using Miningcore.Configuration;
using Miningcore.Extensions;
using Miningcore.Mining;
using Miningcore.Time;

namespace Miningcore.VarDiff;

public static class VarDiffManager
{
    private const int BufferSize = 10;      // Last 10 shares should be enough
    private const int SafetyMargin = 1;       // ensure we don't miss a cycle due to sub-second fraction delta

    public static int? Update(WorkerContextBase context, VarDiffConfig options, IMasterClock clock)
    {
        var ctx = context.VarDiff;
        // Assume context.Difficulty is now an int.
        int difficulty = context.Difficulty;
        int ts = (int)clock.Now.ToUnixSeconds();

        try
        {
            Monitor.Enter(ctx);

            if (ctx.LastTs.HasValue)
            {
                int minDiff = options.MinDiff;
                // If options.MaxDiff is null then default to int.MaxValue (regtest or unlimited)
                int maxDiff = options.MaxDiff ?? int.MaxValue;
                int timeDelta = ts - ctx.LastTs.Value;

                // Make sure the buffer exists at this point
                ctx.TimeBuffer ??= new CircularBuffer<int>(BufferSize);

                // Always calculate the time until now even if there is no share submitted.
                int timeTotal = ctx.TimeBuffer.Sum() + timeDelta;
                int avg = timeTotal / (ctx.TimeBuffer.Size + 1);

                if (avg is 0) return null;

                // Once a share is submitted, store the time into the buffer and update the last time.
                ctx.TimeBuffer.PushBack(timeDelta);
                ctx.LastTs = ts;

                // Check if we need to change the difficulty.
                // Compute variance using integer math (truncation may occur).
                int variance = options.TargetTime * options.VariancePercent / 100;
                int tMin = options.TargetTime - variance;
                int tMax = options.TargetTime + variance;

                // Only change diff if enough time has passed AND the average is outside acceptable bounds.
                if (ts - ctx.LastRetarget < options.RetargetTime || (avg >= tMin && avg <= tMax))
                    return null;

                // Compute the new difficulty (integer division; consider rounding if needed)
                int newDiff = difficulty * options.TargetTime / avg;

                if (TryApplyNewDiff(ref newDiff, difficulty, minDiff, maxDiff, ts, ctx, options, clock))
                    return newDiff;
            }
            else
            {
                // Initialization
                ctx.LastRetarget = ts;
                ctx.LastTs = ts;
            }
        }
        finally
        {
            Monitor.Exit(ctx);
        }

        return null;
    }

    public static int? IdleUpdate(WorkerContextBase context, VarDiffConfig options, IMasterClock clock)
    {
        var ctx = context.VarDiff;
        int difficulty = context.Difficulty;

        // Abort if a regular update is just happening.
        if (!Monitor.TryEnter(ctx))
            return null;

        try
        {
            int ts = (int)clock.Now.ToUnixSeconds();
            int timeDelta;

            if (ctx.LastTs.HasValue)
                timeDelta = ts - ctx.LastTs.Value;
            else
                timeDelta = ts - (int)ctx.Created.ToUnixSeconds();

            // Apply the safety margin.
            timeDelta += SafetyMargin;

            // Only get involved if the last update happened longer than retargetTime ago.
            if (timeDelta < options.RetargetTime)
                return null;

            // Update the last time.
            ctx.LastTs = ts;

            int minDiff = options.MinDiff;
            int maxDiff = options.MaxDiff ?? int.MaxValue;

            // Always calculate the time until now even if there is no share submitted.
            int timeTotal = (ctx.TimeBuffer?.Sum() ?? 0) + (timeDelta - SafetyMargin);
            int avg = timeTotal / ((ctx.TimeBuffer?.Size ?? 0) + 1);

            // Compute the possible new difficulty.
            int newDiff = difficulty * options.TargetTime / avg;

            if (TryApplyNewDiff(ref newDiff, difficulty, minDiff, maxDiff, ts, ctx, options, clock))
                return newDiff;
        }
        finally
        {
            Monitor.Exit(ctx);
        }

        return null;
    }

    /// <summary>
    /// Assumes the lock is held on the context.
    /// </summary>
    private static bool TryApplyNewDiff(ref int newDiff, int oldDiff, int minDiff, int maxDiff, int ts,
        VarDiffContext ctx, VarDiffConfig options, IMasterClock clock)
    {
        // If a maximum delta is defined and > 0, limit the amount of change.
        if (options.MaxDelta.HasValue && options.MaxDelta.Value > 0)
        {
            int maxDelta = options.MaxDelta.Value;
            int delta = Math.Abs(newDiff - oldDiff);

            if (delta > maxDelta)
            {
                if (newDiff > oldDiff)
                    newDiff -= delta - maxDelta;
                else if (newDiff < oldDiff)
                    newDiff += delta - maxDelta;
            }
        }

        // Clamp the new difficulty to the valid range.
        if (newDiff < minDiff)
            newDiff = minDiff;
        if (newDiff > maxDiff)
            newDiff = maxDiff;

        // If the difficulty is unchanged, do nothing.
        if (newDiff == oldDiff)
            return false;

        ctx.LastRetarget = ts;
        ctx.LastUpdate = clock.Now;

        // Due to the change of difficulty, clear the buffer.
        if (ctx.TimeBuffer != null)
            ctx.TimeBuffer = new CircularBuffer<int>(BufferSize);

        return true;
    }
}
