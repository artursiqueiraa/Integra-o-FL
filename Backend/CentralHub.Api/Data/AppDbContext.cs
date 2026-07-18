using CentralHub.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace CentralHub.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Building> Buildings => Set<Building>();
    public DbSet<Central> Centrals => Set<Central>();
    public DbSet<History> Histories => Set<History>();
    public DbSet<CentralSession> CentralSessions => Set<CentralSession>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Building>(entity =>
        {
            entity.Property(b => b.Nome).IsRequired().HasMaxLength(150);
            entity.Property(b => b.Descricao).HasMaxLength(500);
        });

        modelBuilder.Entity<Central>(entity =>
        {
            entity.Property(c => c.Nome).IsRequired().HasMaxLength(150);
            entity.Property(c => c.IP).IsRequired().HasMaxLength(45);
            entity.Property(c => c.Usuario).IsRequired().HasMaxLength(100);
            entity.Property(c => c.Senha).IsRequired().HasMaxLength(200);
            entity.Property(c => c.Fabricante).HasMaxLength(100);
            entity.Property(c => c.Modelo).HasMaxLength(100);
            entity.Property(c => c.Firmware).HasMaxLength(100);
            entity.Property(c => c.Status).HasMaxLength(50);
            entity.Property(c => c.NumeroSerie).HasMaxLength(10);
            entity.Property(c => c.UltimoIpConectado).HasMaxLength(45);
            entity.HasIndex(c => c.NumeroSerie).IsUnique();

            entity.HasOne(c => c.Building)
                  .WithMany()
                  .HasForeignKey(c => c.BuildingId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<History>(entity =>
        {
            entity.Property(h => h.Comando).IsRequired().HasMaxLength(50);
            entity.Property(h => h.Resultado).HasMaxLength(500);

            entity.HasOne(h => h.Central)
                  .WithMany()
                  .HasForeignKey(h => h.CentralId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<CentralSession>(entity =>
        {
            entity.Property(s => s.NumeroSerie).IsRequired().HasMaxLength(10);
            entity.Property(s => s.Imei).HasMaxLength(15);
            entity.Property(s => s.Mac).HasMaxLength(12);
            entity.Property(s => s.ModeloNome).HasMaxLength(50);
            entity.Property(s => s.VersaoFirmware).HasMaxLength(20);
            entity.Property(s => s.EnderecoRemoto).IsRequired().HasMaxLength(64);
            entity.HasIndex(s => s.NumeroSerie);

            entity.HasOne(s => s.Central)
                  .WithMany()
                  .HasForeignKey(s => s.CentralId)
                  .OnDelete(DeleteBehavior.SetNull);
        });
    }
}
