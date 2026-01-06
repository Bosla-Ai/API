namespace Shared.Parameters;

public sealed class RefreshTokenParameters
{
    public string? UserId { get; set; }
    public Guid? DeviceId { get; set; }
}