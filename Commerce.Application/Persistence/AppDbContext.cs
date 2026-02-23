using Microsoft.EntityFrameworkCore;

namespace Commerce.Application.Persistence;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<string> strings { get; set; }
}