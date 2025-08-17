using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Domain.Entities;
using Domain.Responses;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace Service.Helpers;

public class AuthenticationHelper
{
    private readonly IConfiguration _configuration;
    protected APIResponse _response;
    public AuthenticationHelper(IConfiguration configuration)
    {
        _configuration = configuration;
        _response = new APIResponse();
    }

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
            Encoding.UTF8.GetBytes(_configuration["JWT:Key"]!)
        );

        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var jwtToken = new JwtSecurityToken(
            issuer: _configuration["JWT:Issuer"],
            audience: _configuration["JWT:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(Convert.ToDouble(_configuration["JWT:ExpiresAt"]!)),
            signingCredentials: creds
        );

        var token = new JwtSecurityTokenHandler().WriteToken(jwtToken);
        return (token, jwtToken);
    }

    public List<string> AddIdentityErrors(IdentityResult result)
    {
        return result.Errors.Select(e => e.Description).ToList();
    }
}