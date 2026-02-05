using System.Security.Claims;
using UTXO_E_Mail_Agent_Admintool.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;

namespace UTXO_E_Mail_Agent_Admintool.Controllers;

[Controller]
[Route("auth")]
public class AccountController : Controller
{
    private readonly AuthService _authService;

    public AccountController(AuthService authService)
    {
        _authService = authService;
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromForm] string username, [FromForm] string password)
    {
        Console.WriteLine($"[FORM LOGIN] Login attempt for: {username}");
        Console.WriteLine($"[FORM LOGIN] Request Scheme: {HttpContext.Request.Scheme}");
        Console.WriteLine($"[FORM LOGIN] Request Host: {HttpContext.Request.Host}");

        var admin = await _authService.ValidateUserAsync(username, password);

        if (admin != null)
        {
            Console.WriteLine($"[FORM LOGIN] User validated: {username}");

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, username),
                new Claim(ClaimTypes.Role, "Administrator"),
                new Claim("UserId", admin.Id.ToString())
            };

            var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var claimsPrincipal = new ClaimsPrincipal(claimsIdentity);

            var authProperties = new AuthenticationProperties
            {
                IsPersistent = true,
                ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(30),
                AllowRefresh = true
            };

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                claimsPrincipal,
                authProperties);

            Console.WriteLine($"[FORM LOGIN] SignInAsync completed");

            // Check immediately if user is authenticated
            var authResult = await HttpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            Console.WriteLine($"[FORM LOGIN] Auth check after SignIn - Success: {authResult.Succeeded}");
            Console.WriteLine($"[FORM LOGIN] Auth check after SignIn - IsAuthenticated: {HttpContext.User.Identity?.IsAuthenticated}");
            Console.WriteLine($"[FORM LOGIN] Auth check after SignIn - Name: {HttpContext.User.Identity?.Name}");

            // Check cookies
            Console.WriteLine($"[FORM LOGIN] Response cookies count: {HttpContext.Response.Headers.SetCookie.Count}");
            foreach (var cookie in HttpContext.Response.Headers.SetCookie)
            {
                Console.WriteLine($"[FORM LOGIN] Cookie: {cookie}");
            }

            // Redirect to home page
            return Redirect("/");
        }

        Console.WriteLine($"[FORM LOGIN] User validation failed for: {username}");
        // Redirect back to login with error
        return Redirect("/login?error=1");
    }

    [HttpGet("check")]
    public async Task<IActionResult> CheckAuth()
    {
        Console.WriteLine($"[API CHECK] Request Cookies count: {HttpContext.Request.Cookies.Count}");
        foreach (var cookie in HttpContext.Request.Cookies)
        {
            Console.WriteLine($"[API CHECK] Cookie received: {cookie.Key} = {cookie.Value.Substring(0, Math.Min(50, cookie.Value.Length))}...");
        }

        var authResult = await HttpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        Console.WriteLine($"[API CHECK] AuthenticateAsync result: {authResult.Succeeded}");

        if (authResult.Succeeded)
        {
            Console.WriteLine($"[API CHECK] Principal Name: {authResult.Principal?.Identity?.Name}");
            Console.WriteLine($"[API CHECK] Principal AuthType: {authResult.Principal?.Identity?.AuthenticationType}");
        }

        var isAuth = HttpContext.User.Identity?.IsAuthenticated ?? false;
        var username = HttpContext.User.Identity?.Name ?? "N/A";

        Console.WriteLine($"[API CHECK] HttpContext.User.IsAuthenticated: {isAuth}, Username: {username}");

        return Ok(new {
            isAuthenticated = isAuth,
            username = username,
            authType = HttpContext.User.Identity?.AuthenticationType,
            cookieCount = HttpContext.Request.Cookies.Count,
            authResultSucceeded = authResult.Succeeded
        });
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        Console.WriteLine("[LOGOUT] Signing out user");
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return Redirect("/login");
    }
}

public class LoginRequest
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}
