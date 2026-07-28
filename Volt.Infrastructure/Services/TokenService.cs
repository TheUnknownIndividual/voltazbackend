using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using Volt.Application.Dtos;
using Volt.Application.Interfaces;
using Volt.Domain.Entities;

namespace Volt.Infrastructure.Services
{
    public sealed class TokenService : ITokenService
    {
        private readonly IConfiguration _config;

        public TokenService(IConfiguration config)
        {
            _config = config;
        }
        public TokenDto CreateAdminAccessToken(AdminUser admin)
        {
            var claims = new List<Claim>
            {
                new (ClaimTypes.NameIdentifier, admin.Id.ToString()),
                new (ClaimTypes.Name, admin.Username),
                new (ClaimTypes.Role, admin.Role.ToString()),
                new (JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            };
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["TokenOptions:SecurityKey"]));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
            var token = new JwtSecurityToken(
                issuer: _config["TokenOptions:Issuer"],
                audience: _config["TokenOptions:Audience"],
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(GetAccessTokenLifetimeMinutes()),
                signingCredentials: creds
            );
            var tokenstring = new JwtSecurityTokenHandler().WriteToken(token);

            return new TokenDto(tokenstring);
        }

        public TokenDto CreateCustomerAccessToken(CustomerUser customer)
        {
            var fullName = $"{customer.FirstName} {customer.LastName}".Trim();
            var claims = new List<Claim>
            {
                new (ClaimTypes.NameIdentifier, customer.Id.ToString()),
                new (ClaimTypes.Name, fullName),
                new (ClaimTypes.Email, customer.Email),
                new (ClaimTypes.Role, customer.Role.ToString()),
                new (JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            };

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["TokenOptions:SecurityKey"]));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
            var token = new JwtSecurityToken(
                issuer: _config["TokenOptions:Issuer"],
                audience: _config["TokenOptions:Audience"],
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(GetAccessTokenLifetimeMinutes()),
                signingCredentials: creds
            );

            var tokenstring = new JwtSecurityTokenHandler().WriteToken(token);
            return new TokenDto(tokenstring);
        }

        private int GetAccessTokenLifetimeMinutes()
            => int.TryParse(_config["TokenOptions:AccessTokenMinutes"], out var minutes)
                ? Math.Max(5, minutes)
                : 60;
    }
}
