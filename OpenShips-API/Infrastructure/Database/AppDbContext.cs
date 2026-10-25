using Microsoft.EntityFrameworkCore;
using OpenShipsAPI.Domain.Destination;
using OpenShipsAPI.Infrastructure.Database.Models;

namespace OpenShipsAPI.Infrastructure.Database;

public class AppDbContext : DbContext
{
    public DbSet<ShipDestination> ShipDestinations => Set<ShipDestination>();

    public DbSet<CurrentAisPosition> CurrentAisPositions => Set<CurrentAisPosition>();

    public DbSet<HistoricalAisPosition> HistoricalAisPositions => Set<HistoricalAisPosition>();

    public DbSet<StaticShipDataAis> StaticShipData => Set<StaticShipDataAis>();

    public DbSet<PortDb> Ports => Set<PortDb>();

    public DbSet<PortAliasDb> PortAliases => Set<PortAliasDb>();

    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ---------------------------------------------------------------------
        // Ship destinations
        // ---------------------------------------------------------------------

        modelBuilder.Entity<ShipDestination>(entity =>
        {
            entity.HasKey(x => x.Id);
            
            entity.HasOne(x => x.FromPort)
                .WithMany()
                .HasForeignKey(x => x.FromPortId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(x => x.ToPort)
                .WithMany()
                .HasForeignKey(x => x.ToPortId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.Property(x => x.Raw)
                .IsRequired();

            entity.Property(x => x.From);

            entity.Property(x => x.To);

            entity.HasIndex(x => new
            {
                x.Mmsi,
                x.LastSeen
            })
            .IsDescending(false, true);
            
            entity.HasIndex(x => new
            {
                x.Mmsi,
                x.Raw
            })
            .IsUnique();
        });

        // ---------------------------------------------------------------------
        // Current AIS positions
        // ---------------------------------------------------------------------

        modelBuilder.Entity<CurrentAisPosition>(entity =>
        {
            entity.HasKey(x => x.Mmsi);

            entity.HasIndex(x => x.EventTimestamp);
        });

        // ---------------------------------------------------------------------
        // Historical AIS positions
        // ---------------------------------------------------------------------

        modelBuilder.Entity<HistoricalAisPosition>(entity =>
        {
            entity.HasKey(x => x.Id);

            entity.HasIndex(x => new
                {
                    x.Mmsi,
                    x.EventTimestamp
                })
                .IsDescending(false, true);
        });

        // ---------------------------------------------------------------------
        // Static AIS data
        // ---------------------------------------------------------------------

        modelBuilder.Entity<StaticShipDataAis>(entity =>
        {
            entity.HasKey(x => x.Mmsi);

            entity.HasIndex(x => x.ShipType);

            entity.HasIndex(x => x.ImoNumber);
        });

        // ---------------------------------------------------------------------
        // Ports
        // ---------------------------------------------------------------------

        modelBuilder.Entity<PortDb>(entity =>
        {
            entity.HasKey(x => x.Id);

            entity.Property(x => x.Country)
                .HasMaxLength(2)
                .IsRequired();

            entity.Property(x => x.Location)
                .HasMaxLength(3)
                .IsRequired();

            entity.Property(x => x.Name)
                .IsRequired();

            entity.Property(x => x.Function)
                .HasMaxLength(8)
                .IsRequired();

            // Country + Location = UN/LOCODE
            entity.HasIndex(x => new
            {
                x.Country,
                x.Location
            })
            .IsUnique();

            entity.HasIndex(x => new
            {
                x.Latitude,
                x.Longitude
            });
        });

        // ---------------------------------------------------------------------
        // Port aliases
        // ---------------------------------------------------------------------

        modelBuilder.Entity<PortAliasDb>(entity =>
        {
            entity.HasKey(x => x.Id);

            entity.Property(x => x.Alias)
                .IsRequired();

            entity.HasOne(x => x.Port)
                .WithMany(x => x.Aliases)
                .HasForeignKey(x => x.PortId)
                .OnDelete(DeleteBehavior.Cascade);
            
            entity.HasIndex(x => new
            {
                x.PortId,
                x.Alias
            })
            .IsUnique();
        });
    }
}