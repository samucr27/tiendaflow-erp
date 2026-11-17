namespace TiendaFlow.Domain;

public class InvalidEmploymentContractException : Exception
{
    public InvalidEmploymentContractException(string message) : base(message) { }
}

public class EmploymentContractNotFoundException : Exception
{
    public EmploymentContractNotFoundException(int id)
        : base($"Employment contract {id} was not found.") { }
}

public class InvalidContractAttachmentException : Exception
{
    public InvalidContractAttachmentException(string message) : base(message) { }
}

public class ContractAttachmentNotFoundException : Exception
{
    public ContractAttachmentNotFoundException(int id)
        : base($"Contract attachment {id} was not found.") { }
}
