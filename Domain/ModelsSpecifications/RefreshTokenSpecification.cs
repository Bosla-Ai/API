using Domain.Contracts;
using Domain.Entities;
using Shared.Parameters;

namespace Domain.ModelsSpecifications;

public class RefreshTokenSpecification : Specifications<RefreshToken>
{

    public RefreshTokenSpecification(RefreshTokenParameters refreshTokenParameters)
        : base(r => (refreshTokenParameters.UserId == null || r.UserId == refreshTokenParameters.UserId)
                    && (r.DeviceId == null || r.DeviceId == refreshTokenParameters.DeviceId)
                    && (!r.IsRevoked)
        )
    {
        
    }
}