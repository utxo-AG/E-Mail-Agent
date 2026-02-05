using System;
using System.Security.Cryptography;
using System.Text;

class GenerateHash
{
    static void Main()
    {
        string password = "HalloPassword123!";
        using var sha256 = SHA256.Create();
        var hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
        string hash = Convert.ToBase64String(hashedBytes);
        Console.WriteLine($"Password: {password}");
        Console.WriteLine($"SHA256 Hash: {hash}");
    }
}
