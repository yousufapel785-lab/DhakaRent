using DhakaRent.Entities.Models;

namespace DhakaRent.Facade.Interfaces;

public interface IRentalOperations
{
    Task<List<LeaseAgreement>> GetLeasesAsync(string userId, bool owner);
    Task StartAsync(string ownerId, int unitId, string tenantId, DateOnly start, decimal rent, decimal deposit, int dueDay);
    Task EndAsync(string ownerId, int leaseId, DateOnly end);
    Task BillAsync(string ownerId, int leaseId, DateOnly month, decimal water, decimal gas, decimal power, decimal service, MeterReading? meter = null);
    Task ConfirmAsync(string ownerId, int invoiceId, decimal amount, string reference);
    Task<ManualPayment?> ReceiptAsync(string actorId, int paymentId);
    Task OpenTicketAsync(string tenantId, int leaseId, string description);
    Task CloseTicketAsync(string ownerId, int ticketId, decimal expense);
}
