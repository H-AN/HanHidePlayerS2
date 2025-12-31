using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Natives;
using SwiftlyS2.Shared.Players;

namespace HanHidePlayerS2;

public class HanHidePlayerHelpers
{
    private readonly ISwiftlyCore _core;
    private readonly ILogger<HanHidePlayerHelpers> _logger;
    public HanHidePlayerHelpers(ISwiftlyCore core,
        ILogger<HanHidePlayerHelpers> logger)
    {
        _core = core;
        _logger = logger;
    }

    public float DistanceSquared(Vector a, Vector b)
    {
        float dx = a.X - b.X;
        float dy = a.Y - b.Y;
        float dz = a.Z - b.Z;
        return dx * dx + dy * dy + dz * dz;
    }

    

}