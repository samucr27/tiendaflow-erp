namespace TiendaFlow.Application.Security;

/// <summary>
/// Hides the password hashing library from the Application layer. The web
/// project implements it with ASP.NET Core's PasswordHasher.
/// </summary>
public interface IPasswordHasher
{
    string Hash(string password);

    bool Verify(string passwordHash, string password);
}
