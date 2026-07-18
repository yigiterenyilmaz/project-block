// PURPOSE: Placeholder for the market ("market") phase that opens after every round.
// CONFIRMED DESIGN (to implement later): jokers, powers and blocks can be bought;
// held jokers/powers can be sold; the currency is TotalScore (the same points the
// player scores with). For now the market offers nothing and the only action is
// GameSession.LeaveMarket().

namespace ProjectBlock.Core
{
    /// <summary>Empty market. Future: offer generation, buy/sell, pricing, rerolls.</summary>
    public sealed class MarketStub
    {
    }
}
