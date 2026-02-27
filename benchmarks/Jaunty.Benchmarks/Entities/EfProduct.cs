using System.Data.Common;

using Microsoft.EntityFrameworkCore;

using Pomelo.EntityFrameworkCore.MySql.Infrastructure;

namespace Jaunty.Benchmarks.Entities;

public class EfProduct
{
    public int product_id { get; set; }
    public string product_name { get; set; } = null!;
    public decimal unit_price { get; set; }
    public int units_in_stock { get; set; }
    public bool discontinued { get; set; }
}

public class BenchmarkDbContext : DbContext
{
    private readonly string _connectionString;
    private readonly string _provider;

    public DbSet<EfProduct> BenchmarkProducts => Set<EfProduct>();

    public BenchmarkDbContext(DbConnection connection, string provider)
    {
        _connectionString = connection.ConnectionString;
        _provider = provider;
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        switch (_provider)
        {
            case "sqlite":
                optionsBuilder.UseSqlite(_connectionString);
                break;
            case "sqlserver":
                optionsBuilder.UseSqlServer(_connectionString);
                break;
            case "postgresql":
                optionsBuilder.UseNpgsql(_connectionString);
                break;
            case "mariadb":
                optionsBuilder.UseMySql(_connectionString, ServerVersion.AutoDetect(_connectionString));
                break;
        }

        optionsBuilder.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<EfProduct>(entity =>
        {
            entity.ToTable("benchmark_products");
            entity.HasKey(e => e.product_id);
        });
    }
}
