namespace Domain.Requests;

public sealed class RefreshRequest
{
    public string RefreshToken { get; set; }
    public Guid DeviceId { get; set; }
}