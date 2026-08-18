using PortfolioCalc.Core.Domain;

namespace PortfolioCalc.App.Application.Positions;

/// <summary>A <see cref="Position"/> currently held (net quantity &gt; 0), as derived from
/// its transaction history by <see cref="PositionValuationService.GetCurrentPositionsAsync"/>.
/// A fully-sold position (net quantity == 0) never produces one of these.</summary>
public sealed record HeldPosition(Position Position, decimal Quantity);
