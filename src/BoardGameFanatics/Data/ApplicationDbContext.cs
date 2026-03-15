using BoardGameFanatics.Models;
using Microsoft.EntityFrameworkCore;

namespace BoardGameFanatics.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : DbContext(options)
{
    public DbSet<Player> Players => Set<Player>();
}
