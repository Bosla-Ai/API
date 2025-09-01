namespace Domain.Requests;

public sealed class LogoutForAllRequest
{
    public Guid DeviceId { get; set; }
}