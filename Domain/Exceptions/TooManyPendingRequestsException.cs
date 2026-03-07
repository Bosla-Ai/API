namespace Domain.Exceptions;

public class TooManyPendingRequestsException(int maxPending)
    : Exception($"Too many pending AI requests. Maximum {maxPending} concurrent requests allowed.")
{
    public int MaxPending { get; } = maxPending;
}
