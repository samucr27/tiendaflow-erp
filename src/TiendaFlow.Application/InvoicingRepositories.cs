using TiendaFlow.Domain;

namespace TiendaFlow.Application;

public interface IInvoiceRepository
{
    SalesInvoice? GetById(int id);
    void Add(SalesInvoice invoice);
    void Update(SalesInvoice invoice);
}

public interface IInvoiceQuery
{
    IReadOnlyList<InvoiceRow> List();
    InvoiceDetail? GetById(int id);
}
