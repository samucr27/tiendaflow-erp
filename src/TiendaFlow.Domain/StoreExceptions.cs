namespace TiendaFlow.Domain;

public class InvalidStoreException : Exception
{
    public InvalidStoreException(string message) : base(message) { }
}

public class StoreNotFoundException : Exception
{
    public StoreNotFoundException(int storeId) : base($"Store {storeId} was not found.") { }
}

public class DuplicateStoreNameException : Exception
{
    public DuplicateStoreNameException(string name)
        : base($"Store name {name} is already registered.") { }
}

/// <summary>
/// The store cannot be deactivated while it still holds units, open purchase
/// orders or active users: deactivating it would hide live operational data.
/// </summary>
public class StoreHasStockException : Exception
{
    public StoreHasStockException(int storeId)
        : base($"Store {storeId} still has stock and cannot be deactivated.") { }
}

public class StoreHasOpenOrdersException : Exception
{
    public StoreHasOpenOrdersException(int storeId)
        : base($"Store {storeId} still has open purchase orders and cannot be deactivated.") { }
}

public class StoreHasActiveUsersException : Exception
{
    public StoreHasActiveUsersException(int storeId)
        : base($"Store {storeId} still has active users and cannot be deactivated.") { }
}
