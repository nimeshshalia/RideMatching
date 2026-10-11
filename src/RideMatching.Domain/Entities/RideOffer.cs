using RideMatching.Domain.Common;
using RideMatching.Domain.Enums;

namespace RideMatching.Domain.Entities;

public class RideOffer : BaseEntity<Guid>
{
    public Guid RideId { get; private set; }
    public Guid DriverId { get; private set; }
    public DateTime OfferedAtUtc { get; private set; }
    public DateTime ExpiresAtUtc { get; private set; }
    public OfferStatus Status { get; private set; } = OfferStatus.Pending;
    public DateTime? RespondedAtUtc { get; private set; }

    protected RideOffer() { }

    public RideOffer(Guid rideId, Guid driverId, DateTime expiresAtUtc)
    {
        Id = Guid.NewGuid();
        RideId = rideId;
        DriverId = driverId;
        OfferedAtUtc = DateTime.UtcNow;
        ExpiresAtUtc = expiresAtUtc;
        Status = OfferStatus.Pending;
    }

    public void Accept()
    {
        Status = OfferStatus.Accepted;
        RespondedAtUtc = DateTime.UtcNow;
    }

    public void Reject()
    {
        Status = OfferStatus.Rejected;
        RespondedAtUtc = DateTime.UtcNow;
    }

    public void Expire()
    {
        if (Status == OfferStatus.Pending)
        {
            Status = OfferStatus.Expired;
            RespondedAtUtc = DateTime.UtcNow;
        }
    }
}
