namespace Domain.Requests;

public sealed class LogoutRequest
{
    public Guid DeviceId { get; set; }
}