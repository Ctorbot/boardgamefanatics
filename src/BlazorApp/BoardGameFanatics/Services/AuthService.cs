using System.Security.Claims;
using BoardGameFanatics.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;

namespace BoardGameFanatics.Services;

public class AuthService(
    Supabase.Client supabase,
    AppDbContext db,
    IHttpContextAccessor httpContextAccessor)
{
    public async Task<(bool Success, string? Error)> SignInAsync(string email, string password)
    {
        try
        {
            var session = await supabase.Auth.SignIn(email, password);
            if (session?.User?.Id is null)
                return (false, "Invalid credentials.");

            var playerId = Guid.Parse(session.User.Id);
            var player = await db.Players.FindAsync(playerId);
            if (player is null)
                return (false, "Account not found. Please contact an administrator.");

            await IssueCookieAsync(player);
            return (true, null);
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    public async Task<(bool Success, string? Error)> SignUpAsync(string email, string password, string displayName)
    {
        try
        {
            await supabase.Auth.SignUp(email, password, new Supabase.Gotrue.SignUpOptions
            {
                Data = new Dictionary<string, object> { { "display_name", displayName } }
            });
            return (true, null);
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    public async Task SignOutAsync()
    {
        var httpContext = httpContextAccessor.HttpContext;
        if (httpContext is null) return;
        await httpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        try { await supabase.Auth.SignOut(); } catch { /* best-effort */ }
    }

    private async Task IssueCookieAsync(Player player)
    {
        var httpContext = httpContextAccessor.HttpContext
            ?? throw new InvalidOperationException("No HttpContext available for cookie sign-in.");

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, player.Id.ToString()),
            new("displayName", player.DisplayName),
            new("status", player.Status.ToString()),
            new(ClaimTypes.Role, player.Role.ToString()),
        };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme));
        await httpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal,
            new AuthenticationProperties { IsPersistent = true, ExpiresUtc = DateTimeOffset.UtcNow.AddDays(7) });
    }

    public static Player? GetCurrentPlayer(ClaimsPrincipal user)
    {
        if (user.Identity?.IsAuthenticated != true) return null;
        var id = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(id, out var guid)) return null;

        return new Player
        {
            Id = guid,
            DisplayName = user.FindFirst("displayName")?.Value ?? "",
            Status = Enum.TryParse<PlayerStatus>(user.FindFirst("status")?.Value, out var s) ? s : PlayerStatus.Pending,
            Role = Enum.TryParse<PlayerRole>(user.FindFirst(ClaimTypes.Role)?.Value, out var r) ? r : PlayerRole.Player,
        };
    }
}
