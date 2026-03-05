
using System;
using ItauCorretora.Desafio.Models;
using Microsoft.EntityFrameworkCore;
using DbContext = Microsoft.EntityFrameworkCore.DbContext;

namespace ItauCorretora.Desafio.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Customer> Customers { get; set; }
    public DbSet<Account> Accounts { get; set; }
    public DbSet<Stock> Stocks { get; set; }
    public DbSet<Quote> Quotes { get; set; }
    public DbSet<RecommendedWallet> RecommendedWallets { get; set; }
    public DbSet<WalletRecommendedItem> WalletRecommendedItems { get; set; }
    public DbSet<Order> Orders { get; set; }
    public DbSet<AccountMovement> AccountMovements { get; set; }
    public DbSet<CustomerPosition> CustomerPositions { get; set; }
    public DbSet<IncomeTax> IncomeTaxes { get; set; }

    public DbSet<MasterCustody> MasterCustodies { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Customer -> Account (1:1)
        modelBuilder.Entity<Customer>()
            .HasOne(c => c.Account)
            .WithOne(a => a.Customer)
            .HasForeignKey<Account>(a => a.CustomerId)
            .OnDelete(DeleteBehavior.Cascade);

        // Customer -> Orders
        modelBuilder.Entity<Customer>()
            .HasMany(c => c.Orders)
            .WithOne(o => o.Customer)
            .HasForeignKey(o => o.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        // Customer -> Positions
        modelBuilder.Entity<Customer>()
            .HasMany(c => c.Positions)
            .WithOne(p => p.Customer)
            .HasForeignKey(p => p.CustomerId)
            .OnDelete(DeleteBehavior.Cascade);

        // Account -> Movements
        modelBuilder.Entity<Account>()
            .HasMany(a => a.Movements)
            .WithOne()
            .HasForeignKey(m => m.AccountId)
            .OnDelete(DeleteBehavior.Cascade);

        // Stock -> Quotes
        modelBuilder.Entity<Stock>()
            .HasMany(s => s.Quotes)
            .WithOne(q => q.Stock)
            .HasForeignKey(q => q.StockId)
            .OnDelete(DeleteBehavior.Cascade);

        // RecommendedWallet -> Items
        modelBuilder.Entity<RecommendedWallet>()
            .HasMany(rw => rw.Itens)
            .WithOne(i => i.RecommendedWallet)
            .HasForeignKey(i => i.RecommendedWalletId)
            .OnDelete(DeleteBehavior.Cascade);

        // WalletRecommendedItem -> Stock
        modelBuilder.Entity<WalletRecommendedItem>()
            .HasOne(i => i.Stock)
            .WithMany(s => s.WalletItems)
            .HasForeignKey(i => i.StockId)
            .OnDelete(DeleteBehavior.Restrict);

        // Order indexes
        modelBuilder.Entity<Order>()
            .HasIndex(o => o.CustomerId);
        modelBuilder.Entity<Order>()
            .HasIndex(o => o.StockId);
        modelBuilder.Entity<Order>()
            .HasIndex(o => o.Status);

        // AccountMovement -> Order 
        modelBuilder.Entity<AccountMovement>()
            .HasOne(m => m.Order)
            .WithMany()
            .HasForeignKey(m => m.OrderId)
            .OnDelete(DeleteBehavior.SetNull);

        // CustomerPosition unique index (a customer can only have one position per stock)
        modelBuilder.Entity<CustomerPosition>()
            .HasIndex(p => new { p.CustomerId, p.StockId })
            .IsUnique();

        // Quote unique index (only one quote per stock per date)
        modelBuilder.Entity<Quote>()
            .HasIndex(q => new { q.StockId, q.Date })
            .IsUnique();

        // MasterCustody unique index (ensures there's only one master custody record per stock)
        modelBuilder.Entity<MasterCustody>()
            .HasIndex(m => m.StockId)
            .IsUnique();

        // Order -> Customer (opcional)
        modelBuilder.Entity<Order>()
            .HasOne(o => o.Customer)
            .WithMany(c => c.Orders)
            .HasForeignKey(o => o.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        // Order -> Account (opcional)
        modelBuilder.Entity<Order>()
            .HasOne(o => o.Account)
            .WithMany(a => a.Orders)
            .HasForeignKey(o => o.AccountId)
            .OnDelete(DeleteBehavior.Restrict);

        base.OnModelCreating(modelBuilder);
    }
}