using PortfolioCalc.Core.Domain;

namespace PortfolioCalc.Core.Repositories;

public interface IPositionRepository
{
    Task<Position> AddAsync(Position position);
    Task<Position?> GetByIdAsync(int id);
    Task DeleteAsync(int id);
}
