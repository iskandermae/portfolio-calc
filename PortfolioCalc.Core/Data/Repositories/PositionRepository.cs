using Microsoft.EntityFrameworkCore;
using PortfolioCalc.Core.Domain;
using PortfolioCalc.Core.Repositories;

namespace PortfolioCalc.Core.Data.Repositories;

public class PositionRepository(PortfolioDbContext context) : IPositionRepository
{
    public async Task<Position> AddAsync(Position position)
    {
        context.Positions.Add(position);
        await context.SaveChangesAsync();
        return position;
    }

    public Task<Position?> GetByIdAsync(int id) =>
        context.Positions.FirstOrDefaultAsync(p => p.Id == id);

    public async Task DeleteAsync(int id)
    {
        var entity = await context.Positions.FindAsync(id);
        if (entity is not null)
        {
            context.Positions.Remove(entity);
            await context.SaveChangesAsync();
        }
    }
}
