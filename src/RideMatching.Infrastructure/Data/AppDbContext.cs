using Microsoft.EntityFrameworkCore;
using RideMatching.Application.Interfaces;
using RideMatching.Domain.Entities;

namespace RideMatching.Infrastructure.Data;

public class AppDbContext : DbContext, IApplicationDbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Driver> Drivers => Set<Driver>();
    public DbSet<Rider> Riders => Set<Rider>();
    public DbSet<Ride> Rides => Set<Ride>();
    public DbSet<RideOffer> RideOffers => Set<RideOffer>();
    public DbSet<DriverLocationHistory> DriverLocationHistories => Set<DriverLocationHistory>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Driver Configuration
        modelBuilder.Entity<Driver>(b =>
        {
            b.HasKey(d => d.Id);
            b.Property(d => d.Name).IsRequired().HasMaxLength(100);
            b.Property(d => d.Email).IsRequired().HasMaxLength(150);
            b.Property(d => d.Phone).IsRequired().HasMaxLength(30);
            b.Property(d => d.VehicleModel).IsRequired().HasMaxLength(100);
            b.Property(d => d.LicensePlate).IsRequired().HasMaxLength(20);
            b.Property(d => d.ConcurrencyStamp).IsConcurrencyToken().HasMaxLength(64);

            b.Property(d => d.CurrentLatitude).HasColumnName("CurrentLatitude");
            b.Property(d => d.CurrentLongitude).HasColumnName("CurrentLongitude");
            b.Ignore(d => d.CurrentLocation);

            b.HasIndex(d => d.Status);
        });

        // Rider Configuration
        modelBuilder.Entity<Rider>(b =>
        {
            b.HasKey(r => r.Id);
            b.Property(r => r.Name).IsRequired().HasMaxLength(100);
            b.Property(r => r.Email).IsRequired().HasMaxLength(150);
            b.Property(r => r.Phone).IsRequired().HasMaxLength(30);
        });

        // Ride Configuration
        modelBuilder.Entity<Ride>(b =>
        {
            b.HasKey(r => r.Id);
            b.Property(r => r.ConcurrencyStamp).IsConcurrencyToken().HasMaxLength(64);
            b.Property(r => r.EstimatedFare).HasPrecision(18, 2);

            b.Property(r => r.PickupLatitude).IsRequired().HasColumnName("PickupLatitude");
            b.Property(r => r.PickupLongitude).IsRequired().HasColumnName("PickupLongitude");
            b.Ignore(r => r.PickupLocation);

            b.Property(r => r.DropoffLatitude).IsRequired().HasColumnName("DropoffLatitude");
            b.Property(r => r.DropoffLongitude).IsRequired().HasColumnName("DropoffLongitude");
            b.Ignore(r => r.DropoffLocation);

            b.HasOne(r => r.Rider)
                .WithMany()
                .HasForeignKey(r => r.RiderId)
                .OnDelete(DeleteBehavior.Restrict);

            b.HasOne(r => r.Driver)
                .WithMany()
                .HasForeignKey(r => r.DriverId)
                .OnDelete(DeleteBehavior.SetNull);

            b.HasMany(r => r.Offers)
                .WithOne()
                .HasForeignKey(o => o.RideId)
                .OnDelete(DeleteBehavior.Cascade);

            b.HasIndex(r => r.Status);
            b.HasIndex(r => r.RiderId);
            b.HasIndex(r => r.DriverId);
        });

        // RideOffer Configuration
        modelBuilder.Entity<RideOffer>(b =>
        {
            b.HasKey(o => o.Id);
            b.Property(o => o.Id).ValueGeneratedNever();
            b.HasIndex(o => new { o.RideId, o.DriverId });
        });

        // DriverLocationHistory Configuration
        modelBuilder.Entity<DriverLocationHistory>(b =>
        {
            b.HasKey(h => h.Id);
            b.Property(h => h.Id).ValueGeneratedOnAdd();
            b.Property(h => h.Latitude).IsRequired();
            b.Property(h => h.Longitude).IsRequired();
            b.Ignore(h => h.Location);

            b.HasIndex(h => h.DriverId);
            b.HasIndex(h => h.TimestampUtc);
        });
    }
}
