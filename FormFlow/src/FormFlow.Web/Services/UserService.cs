using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using FormFlow.Web.Data;
using FormFlow.Web.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace FormFlow.Web.Services
{
    public class UserService
    {
        private readonly FormFlowDbContext _db;

        public UserService(FormFlowDbContext db)
        {
            _db = db;
        }

        public async Task<AppUser> AuthenticateAsync(string username, string password)
        {
            var name = (username ?? string.Empty).Trim();
            var user = await _db.Users.FirstOrDefaultAsync(u => u.Username == name);
            if (user == null || !PasswordHasher.Verify(password, user.PasswordHash, user.PasswordSalt))
            {
                return null;
            }

            return user;
        }

        public static Task SignInAsync(HttpContext httpContext, AppUser user, bool isPersistent)
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString(CultureInfo.InvariantCulture)),
                new Claim(ClaimTypes.Name, user.DisplayName),
                new Claim(Claims.StageRole, user.Role.ToString()),
                new Claim(Claims.IsAdministrator, user.IsAdministrator ? "true" : "false")
            };

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            return httpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(identity),
                new AuthenticationProperties { IsPersistent = isPersistent });
        }

        public static StageRole RoleOf(ClaimsPrincipal principal)
        {
            var value = principal?.FindFirst(Claims.StageRole)?.Value;
            return System.Enum.TryParse<StageRole>(value, out var role) ? role : StageRole.Employee;
        }

        public static bool IsAdministrator(ClaimsPrincipal principal)
        {
            return principal?.FindFirst(Claims.IsAdministrator)?.Value == "true";
        }

        public static string DisplayName(ClaimsPrincipal principal)
        {
            return principal?.FindFirst(ClaimTypes.Name)?.Value ?? "مستخدم";
        }

        public Task<List<AppUser>> GetUsersAsync() => _db.Users.OrderBy(u => u.Username).ToListAsync();
    }
}
