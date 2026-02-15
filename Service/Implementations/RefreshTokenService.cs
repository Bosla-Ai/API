using Domain.Contracts;
using Domain.Entities;
using Domain.ModelsSpecifications;
using Service.Abstraction;
using Shared.Parameters;

namespace Service.Implementations;

public class RefreshTokenService(IUnitOfWork unitOfWork) : IRefreshTokenService
{
    private readonly IUnitOfWork _unitOfWork = unitOfWork;

    public async Task<IEnumerable<RefreshToken>>
        GetAllForUserDeviceNotRevokedAsync(RefreshTokenParameters refreshTokenParameters)
    {
        return await _unitOfWork.GetRepo<RefreshToken, Guid>()
            .GetAllAsync(new RefreshTokenSpecification(refreshTokenParameters));
    }

    public async Task UpdateAsync(RefreshToken refreshToken)
    {
        await _unitOfWork.GetRepo<RefreshToken, Guid>().UpdateAsync(refreshToken);
    }

    public async Task CreateAsync(RefreshToken refreshToken)
    {
        await _unitOfWork.GetRepo<RefreshToken, Guid>().CreateAsync(refreshToken);
    }

    public async Task<RefreshToken> GetWithDeviceIdNotRevokedAsync(RefreshTokenParameters refreshTokenParameters)
    {
        return await _unitOfWork.GetRepo<RefreshToken, Guid>()
            .GetAsync(new RefreshTokenSpecification(refreshTokenParameters));
    }

    public async Task DeleteAsync(RefreshToken refreshToken)
    {
        await _unitOfWork.GetRepo<RefreshToken, Guid>().DeleteAsync(refreshToken);
    }
}