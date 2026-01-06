using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Domain.Contracts;
using Domain.Entities;
using Domain.Responses;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Service.Abstraction;

namespace Service.Helpers;

public class AuthenticationHelper(
    UserManager<ApplicationUser> userManager
    , IRefreshTokenService refreshTokenService
    , IUnitOfWork unitOfWork
    , IConfiguration configuration)
{
    public (string hash, string salt) CreateTokenHashAndSalt(string token)
    {
        var saltBytes = RandomNumberGenerator.GetBytes(16);
        var salt = Convert.ToBase64String(saltBytes);

        using var hmac = new HMACSHA256(saltBytes);
        var hash = Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(token)));

        return (hash, salt);
    }

    public bool VerifyTokenWithSalt(string token, string storedHash, string storedSalt)
    {
        try
        {
            var saltBytes = Convert.FromBase64String(storedSalt);
            using var hmac = new HMACSHA256(saltBytes);
            var computed = hmac.ComputeHash(Encoding.UTF8.GetBytes(token));
            var storedHashBytes = Convert.FromBase64String(storedHash);

            return CryptographicOperations
                .FixedTimeEquals(computed, storedHashBytes);
        }
        catch
        {
            return false;
        }
    }

    public string GeneratePlainRefreshToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(64); // 512 bits
        return Convert.ToBase64String(bytes);
    }

    public (string token, JwtSecurityToken jwtToken) GenerateJwtAccessToken(ApplicationUser user, IList<string> roles)
    {
        var claims = new List<Claim>()
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.UserName!),
            new Claim(ClaimTypes.Email, user.Email!),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        foreach (var role in roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(configuration["JWT:Key"]!)
        );

        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var jwtToken = new JwtSecurityToken(
            issuer: configuration["JWT:Issuer"],
            audience: configuration["JWT:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(Convert.ToDouble(configuration["JWT:Expires"]!)),
            signingCredentials: creds
        );

        var token = new JwtSecurityTokenHandler().WriteToken(jwtToken);
        return (token, jwtToken);
    }

    public List<string> AddIdentityErrors(IdentityResult result)
    {
        return result.Errors.Select(e => e.Description).ToList();
    }

    public async Task<LoginServerResponse>
        GenerateAndStoreTokensAsync(
            ApplicationUser user,
            Guid deviceId)
    {
        var roles = await userManager.GetRolesAsync(user);
        var (jwt, jwtToken) = GenerateJwtAccessToken(user, roles);

        var plainRefresh = GeneratePlainRefreshToken();
        var (hash, salt) = CreateTokenHashAndSalt(plainRefresh);

        var refreshEntity = new RefreshToken
        {
            DeviceId = deviceId,
            TokenHash = hash,
            TokenSalt = salt,
            Created = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(Convert.ToDouble(configuration["JWT:RefreshTokenLifeTime"]!)),
            UserId = user.Id,
            JwtTokenId = jwtToken.Id
        };

        await refreshTokenService.CreateAsync(refreshEntity);

        var loginResponse = new LoginServerResponse
        {
            AccessToken = jwt,
            AccessTokenExpiration = jwtToken.ValidTo,
            RefreshToken = plainRefresh,
            RefreshTokenExpiration = refreshEntity.ExpiresAt,
            FirstName = user.FirstName,
            LastName = user.LastName,
            UserName = user.UserName!,
            Email = user.Email!,
            Role = roles.FirstOrDefault() ?? "",
            DeviceId = deviceId
        };

        return loginResponse;
    }
}