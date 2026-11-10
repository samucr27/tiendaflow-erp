using TiendaFlow.Domain;

namespace TiendaFlow.Application.Security;

public interface IAppUserRepository
{
    AppUser? GetById(int id);

    AppUser? GetByEmail(string email);

    IReadOnlyList<AppUser> List();

    bool ExistsEmail(string email);

    void Add(AppUser user);

    void Update(AppUser user);
}

public interface IEmployeeRepository
{
    Employee? GetById(int id);

    Employee? GetByIdNumber(string idNumber);

    IReadOnlyList<Employee> List();

    void Add(Employee employee);

    void Update(Employee employee);
}

public interface IStoreRepository
{
    Store? GetById(int id);

    IReadOnlyList<Store> List();

    void Add(Store store);

    void Update(Store store);
}

public interface IRoleRepository
{
    Role? GetById(int id);

    IReadOnlyList<Role> List();

    void Add(Role role);
}
