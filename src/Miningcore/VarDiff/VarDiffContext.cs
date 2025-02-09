using CircularBuffer;
using Miningcore.Configuration;

namespace Miningcore.VarDiff;

public class VarDiffContext
{
    public int? LastTs { get; set; }
    public double LastRetarget { get; set; }
    public CircularBuffer<int> TimeBuffer { get; set; }
    public DateTime Created { get; set; }
    public DateTime? LastUpdate { get; set; }
    public VarDiffConfig Config { get; set; }
}
