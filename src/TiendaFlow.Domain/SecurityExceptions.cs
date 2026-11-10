namespace TiendaFlow.Domain;

public class InvalidUserException : Exception
{
    public InvalidUserException(string message) : base(message) { }
}

public class UserNotFoundException : Exception
{
    public UserNotFoundException(int userId) : base($"User {userId} was not found.") { }

    public UserNotFoundException(string email) : base($"User {email} was not found.") { }
}

public class DuplicateEmailException : Exception
{
    public DuplicateEmailException(string email) : base($"Email {email} is already registered.") { }
}

public class DuplicateIdNumberException : Exception
{
    public DuplicateIdNumberException(string idNumber)
        : base($"Identification number {idNumber} is already registered.") { }
}

public class AccessDeniedException : Exception
{
    public AccessDeniedException(string permission)
        : base($"The current user is not allowed to perform {permission}.") => Permission = permission;

    public string Permission { get; }
}
