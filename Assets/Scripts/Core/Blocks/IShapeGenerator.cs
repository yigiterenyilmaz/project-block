// PURPOSE: Produces the next block shape for a deck/market draw. Must be
// deterministic on the given IRandomSource.

using System;
using System.Collections.Generic;

namespace ProjectBlock.Core
{
    /// <summary>Produces block shapes for new cards.</summary>
    public interface IShapeGenerator
    {
        BlockShape NextShape(IRandomSource rng);
    }
}
