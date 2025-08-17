using Domain.Entities;
using Shared.Parameters;

namespace Service.Abstraction;

public interface IRefreshTokenService
{
    Task<IEnumerable<RefreshToken>> 
        GetAllForUserDeviceNotRevokedAsync(RefreshTokenParameters refreshTokenParameters);

    Task UpdateAsync(RefreshToken refreshToken);
    Task CreateAsync(RefreshToken refreshToken);
    Task<RefreshToken> GetWithDeviceIdNotRevokedAsync(RefreshTokenParameters refreshTokenParameters);
}