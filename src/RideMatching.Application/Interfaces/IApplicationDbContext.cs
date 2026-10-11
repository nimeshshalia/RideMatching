using Microsoft.EntityFrameworkCore;
using RideMatching.Domain.Entities;

namespace RideMatching.Application.Interfaces;

public interface IApplicationDbContext
{
    DbSet<Driver> Drivers { get; }
    DbSet<Rider> Riders { get; }
    DbSet<Ride> Rides { get; }
    DbSet<RideOffer> RideOffers { get; }
    DbSet<DriverLocationHistory> DriverLocationHistories { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
