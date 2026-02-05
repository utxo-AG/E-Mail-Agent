using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;

namespace UTXO_E_Mail_Agent_Admintool.Services;

public class PasswordService
{
    private readonly IPasswordHasher<object> _passwordHasher;

    public PasswordService()
    {
        _passwordHasher = new PasswordHasher<object>();
    }

    /// <summary>
    /// Generates a random password
    /// </summary>
    public string GeneratePassword(int length = 12)
    {
        const string validChars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ1234567890!@#$%";
        var random = new byte[length];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(random);
        }

        var password = new StringBuilder(length);
        foreach (byte b in random)
        {
            password.Append(validChars[b % validChars.Length]);
        }

        return password.ToString();
    }

    /// <summary>
    /// Hashes a password using ASP.NET Identity's PasswordHasher
    /// </summary>
    public string HashPassword(string password)
    {
        return _passwordHasher.HashPassword(new object(), password);
    }

    /// <summary>
    /// Verifies a password against a hash
    /// </summary>
    public bool VerifyPassword(string hash, string password)
    {
        if (string.IsNullOrEmpty(hash) || string.IsNullOrEmpty(password))
        {
            return false;
        }

        try
        {
            var result = _passwordHasher.VerifyHashedPassword(new object(), hash, password);
            return result == PasswordVerificationResult.Success;
        }
        catch (Exception)
        {
            // In case of any exception during verification, return false
            return false;
        }
    }
}
