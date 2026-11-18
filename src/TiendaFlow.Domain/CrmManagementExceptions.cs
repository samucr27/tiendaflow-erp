namespace TiendaFlow.Domain;

public class InvalidLoyaltyMovementException : Exception
{
    public InvalidLoyaltyMovementException(string message) : base(message) { }
}

public class LoyaltyReferenceAlreadyUsedException : Exception
{
    public LoyaltyReferenceAlreadyUsedException(string reference)
        : base($"Ya existe un movimiento de puntos con la referencia {reference}.") { }
}

public class InvalidPqrsException : Exception
{
    public InvalidPqrsException(string message) : base(message) { }
}

public class PqrsNotFoundException : Exception
{
    public PqrsNotFoundException(int id) : base($"No se encontró la solicitud {id}.") { }
}

public class InvalidCampaignException : Exception
{
    public InvalidCampaignException(string message) : base(message) { }
}

public class DuplicateCampaignNameException : Exception
{
    public DuplicateCampaignNameException(string name)
        : base($"Ya existe una campaña con el nombre {name}.") { }
}

public class CampaignNotFoundException : Exception
{
    public CampaignNotFoundException(int id) : base($"No se encontró la campaña {id}.") { }
}
