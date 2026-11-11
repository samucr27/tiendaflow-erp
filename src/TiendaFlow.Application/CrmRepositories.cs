using TiendaFlow.Domain;

namespace TiendaFlow.Application;

public interface ICustomerRepository
{
    Customer? GetById(int id);
    Customer? GetByDocument(string documentNumber);
    void Add(Customer customer);
    void Update(Customer customer);
}

public interface ICustomerQuery
{
    IReadOnlyList<CustomerRow> List();
    CustomerRow? GetById(int id);
}
