using Domain.Contracts;
using Domain.Entities;

namespace Domain.ModelsSpecifications;

public class ActiveRefreshTokensSpecification : Specifications<RefreshToken>
{
    public ActiveRefreshTokensSpecification()
        : base(r => !r.IsRevoked && r.ExpiresAt > DateTime.UtcNow)
    {
    }
}
