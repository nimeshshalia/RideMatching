namespace RideMatching.Domain.Enums;

public enum RideStatus
{
    Requested = 0,
    Matching = 1,
    OfferPending = 2,
    Accepted = 3,
    DriverArriving = 4,
    InProgress = 5,
    Completed = 6,
    Cancelled = 7,
    NoDriversAvailable = 8
}
