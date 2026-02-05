using System.Security.Cryptography;
using System.Text;
using UTXO_E_Mail_Agent_Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace UTXO_E_Mail_Agent_Admintool.Services;

public class AuthService
{
    private readonly DefaultdbContext _context;
    private readonly PasswordService _passwordService;

    public AuthService(DefaultdbContext context, PasswordService passwordService)
    {
        _context = context;
        _passwordService = passwordService;
    }

    public async Task<Administrator?> ValidateUserAsync(string username, string password)
    {
        // Validate input parameters
        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
        {
            Console.WriteLine($"[AuthService] Invalid credentials provided (username or password is empty)");
            return null;
        }

        var admin = await _context.Administrators
            .FirstOrDefaultAsync(a => a.Username == username);

        if (admin == null)
        {
            Console.WriteLine($"[AuthService] User not found: {username}");
            return null;
        }

        // Check if account is blocked
        if (admin.State == "blocked")
        {
            Console.WriteLine($"[AuthService] Account is blocked: {username}");
            return null;
        }

        // Check if account is active
        if (admin.State != "active")
        {
            Console.WriteLine($"[AuthService] Account is not active: {username} (State: {admin.State})");
            return null;
        }

        bool passwordValid = false;

        // Try PasswordHasher first (new method)
        if (_passwordService.VerifyPassword(admin.Passwordhash, password))
        {
            passwordValid = true;
        }
        else
        {
            // Fallback to SHA256 for legacy passwords
            var sha256Hash = HashPasswordSHA256(password);
            if (admin.Passwordhash == sha256Hash)
            {
                Console.WriteLine($"[AuthService] User {username} logged in with legacy SHA256 password. Consider migrating.");
                passwordValid = true;
            }
        }

        if (passwordValid)
        {
            // Reset login attempts on successful login
            if (admin.Loginattempts > 0)
            {
                admin.Loginattempts = 0;
                await _context.SaveChangesAsync();
                Console.WriteLine($"[AuthService] Login attempts reset for user: {username}");
            }
            return admin;
        }

        // Password is invalid - increment login attempts
        admin.Loginattempts++;
        Console.WriteLine($"[AuthService] Failed login attempt #{admin.Loginattempts} for user: {username}");

        // Block account after 3 failed attempts
        if (admin.Loginattempts >= 3)
        {
            admin.State = "blocked";
            Console.WriteLine($"[AuthService] Account blocked after 3 failed attempts: {username}");
        }

        await _context.SaveChangesAsync();
        return null;
    }

    private string HashPasswordSHA256(string password)
    {
        using var sha256 = SHA256.Create();
        var hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
        return Convert.ToBase64String(hashedBytes);
    }
}
