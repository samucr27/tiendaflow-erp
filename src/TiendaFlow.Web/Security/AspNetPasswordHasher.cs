using Microsoft.AspNetCore.Identity;
using TiendaFlow.Application.Security;
using TiendaFlow.Domain;

namespace TiendaFlow.Web.Security;

/// <summary>
/// ASP.NET Core's PBKDF2 password hasher, isolated behind the Application
/// abstraction so services never see plain-text passwords or the library.
/// </summary>
public sealed class AspNetPasswordHasher : IPasswordHasher
{
    private static readonly AppUser HashContext =
        new(null, 1, "password-hasher@tiendaflow.local", "not-a-real-hash");

    private readonly PasswordHasher<AppUser> _hasher = new();

    public string Hash(string password) => _hasher.HashPassword(HashContext, password);

    public bool Verify(string passwordHash, string password) =>
        _hasher.VerifyHashedPassword(HashContext, passwordHash, password)
        != PasswordVerificationResult.Failed;
}
