using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Adecco.WW.Packages.WebApi.Services.Client.Internal.Security
{
    /// <summary>
    /// This class is used to validate JWT tokens.
    /// </summary>
    public static class IdentityModelTokensJwtValidator
    {
        private static readonly JwtSecurityTokenHandler _tokenHandler = new();
        private static readonly TokenValidationParameters _validationParams = new()
        {
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = true,
            RequireSignedTokens = false,
            SignatureValidator = (token, parameters) => new JwtSecurityToken(token),
            ClockSkew = TimeSpan.Zero
        };
        /// <summary>
        /// Validates the JWT token.
        /// </summary>
        /// <param name="token"></param>
        /// <returns></returns>
        public static bool Validate(string token)
        {
            if (string.IsNullOrEmpty(token)) return false;

            try
            {
                _tokenHandler.ValidateToken(token, _validationParams, out _);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
