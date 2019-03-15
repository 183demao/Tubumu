﻿using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Tubumu.Modules.Framework.Authorization;
using Tubumu.Modules.Framework.Extensions;
using Tubumu.Modules.Framework.Swagger;

namespace Tubumu.Modules.Framework.Services
{
    /// <summary>
    /// Token Service
    /// </summary>
    public class TokenService : ITokenService
    {
        private readonly JwtSecurityTokenHandler _tokenHandler = new JwtSecurityTokenHandler();
        private readonly TokenValidationSettings _tokenValidationSettings;
        private readonly IDistributedCache _cache;
        private const string CacheKeyFormat = "RefreshToken:{0}";

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="tokenValidationSettings"></param>
        /// <param name="cache"></param>
        public TokenService(
            TokenValidationSettings tokenValidationSettings,
            IDistributedCache cache
            )
        {
            _tokenValidationSettings = tokenValidationSettings;
            _cache = cache;
        }

        /// <summary>
        /// 生成 Access Token
        /// </summary>
        /// <param name="claims"></param>
        /// <returns></returns>
        public string GenerateAccessToken(IEnumerable<Claim> claims)
        {
            var utcNow = DateTime.UtcNow;
            var jwtToken = new JwtSecurityToken(
                _tokenValidationSettings.ValidIssuer,
                _tokenValidationSettings.ValidAudience,
                claims,
                notBefore: utcNow,
                expires: utcNow.AddSeconds(_tokenValidationSettings.ExpiresSeconds),
                signingCredentials: SignatureHelper.GenerateSigningCredentials(_tokenValidationSettings.IssuerSigningKey)
            );

            return new JwtSecurityTokenHandler().WriteToken(jwtToken);
        }

        /// <summary>
        /// 生成 Refresh Token
        /// </summary>
        /// <returns></returns>
        public async Task<string> GenerateRefreshToken(int userId)
        {
            var randomNumber = new byte[32];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(randomNumber);
                var refreshToken = Convert.ToBase64String(randomNumber);
                var cacheKey = CacheKeyFormat.FormatWith(userId);
                await _cache.SetStringAsync(cacheKey, refreshToken, new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(_tokenValidationSettings.ExpiresSeconds + _tokenValidationSettings.ClockSkewSeconds * 2)
                });
                return refreshToken;
            }
        }

        /// <summary>
        /// 获取 Refresh Token
        /// </summary>
        /// <param name="userId"></param>
        /// <returns></returns>
        public async Task<string> GetRefreshToken(int userId)
        {
            var cacheKey = CacheKeyFormat.FormatWith(userId);
            return await _cache.GetStringAsync(cacheKey);
        }

        /// <summary>
        /// 废弃 Refresh Token
        /// </summary>
        /// <param name="userId"></param>
        /// <returns></returns>
        public async Task RevokeRefreshToken(int userId)
        {
            var cacheKey = CacheKeyFormat.FormatWith(userId);
            await _cache.RemoveAsync(cacheKey);
        }

        /// <summary>
        /// 通过过期 Token 获取 ClaimsPrincipal
        /// </summary>
        /// <param name="token"></param>
        /// <returns></returns>
        public ClaimsPrincipal GetPrincipalFromExpiredToken(string token)
        {
            var tokenValidationParameters = new TokenValidationParameters
            {
                ValidateAudience = false, //you might want to validate the audience and issuer depending on your use case
                ValidateIssuer = false,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = SignatureHelper.GenerateSigningKey(_tokenValidationSettings.IssuerSigningKey),
                ValidateLifetime = false //here we are saying that we don't care about the token's expiration date
            };

            var principal = _tokenHandler.ValidateToken(token, tokenValidationParameters, out SecurityToken securityToken);
            var jwtSecurityToken = securityToken as JwtSecurityToken;
            if (jwtSecurityToken == null || !jwtSecurityToken.Header.Alg.Equals(SecurityAlgorithms.HmacSha256, StringComparison.InvariantCultureIgnoreCase))
                throw new SecurityTokenException("Invalid token");

            return principal;
        }
    }
}
