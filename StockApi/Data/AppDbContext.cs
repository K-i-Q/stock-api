using Microsoft.EntityFrameworkCore;
using StockApi.Models;

namespace StockApi.Data;
public class AppDbContext(DbContextOptions<AppDbContext> opts) : DbContext(opts)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<StockEntry> StockEntries => Set<StockEntry>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<User>().HasIndex(u => u.Email).IsUnique();
        b.Entity<Product>().Property(p => p.Price).HasColumnType("numeric(12,2)");
        b.Entity<OrderItem>().Property(i => i.UnitPrice).HasColumnType("numeric(12,2)");
    }
}