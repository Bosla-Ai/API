namespace Domain.Requests;

public sealed class LogoutForAllRequest
{
    public string UserId { get; set; }
    public Guid DeviceId { get; set; }
}