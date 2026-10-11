namespace RideMatching.Domain.Exceptions;

public class RideDomainException : Exception
{
    public RideDomainException(string message) : base(message) { }
    public RideDomainException(string message, Exception innerException) : base(message, innerException) { }
}

public class ConcurrencyConflictException : RideDomainException
{
    public ConcurrencyConflictException(string message) : base(message) { }
}

public class InvalidStateTransitionException : RideDomainException
{
    public InvalidStateTransitionException(string message) : base(message) { }
}
