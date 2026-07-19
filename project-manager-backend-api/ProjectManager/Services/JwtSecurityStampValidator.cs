using Microsoft.AspNetCore.Identity;
using ProjectManager.Models;
using System.Security.Claims;

namespace ProjectManager.Services
{
    public class JwtSecurityStampValidator
    {
        public const string SecurityStampClaimType = "security_stamp";

        private readonly UserManager<ApplicationUser> _userManager;

        public JwtSecurityStampValidator(UserManager<ApplicationUser> userManager)
        {
            _userManager = userManager;
        }

        public async Task<bool> IsCurrentAsync(ClaimsPrincipal principal)
        {
            var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier);
            var tokenSecurityStamp = principal.FindFirstValue(SecurityStampClaimType);

            if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(tokenSecurityStamp))
            {
                return false;
            }

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                return false;
            }

            var currentSecurityStamp = await _userManager.GetSecurityStampAsync(user);
            return string.Equals(tokenSecurityStamp, currentSecurityStamp, StringComparison.Ordinal);
        }
    }
}
