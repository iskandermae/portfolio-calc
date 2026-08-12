using PortfolioCalc.Core.Domain;

namespace PortfolioCalc.Core.Repositories;

public interface IPositionRepository
{
    Task<Position> AddAsync(Position position);
    Task<Position?> GetByIdAsync(int id);
    /// <summary>Looks up the (unique) Position for an Account×Security pair — used by the
    /// IBKR import to match/auto-create the Position a SecurityTransaction attaches to.</summary>
    Task<Position?> GetByAccountAndSecurityAsync(int accountId, int securityId);
    Task DeleteAsync(int id);
}
