namespace Domain.Entities;

public sealed class RefreshToken
{
    public int Id { get; set; }
    public string UserId { get; set; }

    public Guid DeviceId { get; set; } //also the same SessionId

    public string TokenHash { get; set; } = null!;
    public string TokenSalt { get; set; } = null!; 

    public DateTime Created { get; set; }
    public DateTime ExpiresAt { get; set; }

    public bool IsRevoked { get; set; }
    public DateTime? RevokedAt { get; set; }
    public string? RevokedReason { get; set; }

    public string? ReplacedByTokenHash { get; set; }
    public string? JwtTokenId { get; set; }

    public ApplicationUser User { get; set; }
}