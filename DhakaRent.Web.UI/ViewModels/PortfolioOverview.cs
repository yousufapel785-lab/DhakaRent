using DhakaRent.Entities.Models;

namespace DhakaRent.Web.UI.ViewModels;

public record BuildingSummary(int Id, string Name, int Units, int Occupied, int ActiveLeases, decimal Billed,
    decimal Collected, decimal Outstanding, decimal Overdue, decimal Expenses, int OpenTickets, int Vacant, decimal MonthlyRent, decimal ReceivedThisMonth);
public record StaffAccess(int Id, string Building, string Name, bool Repairs, bool Accounts);
public record UnallocatedExpense(int Id, string Name, decimal Amount);
public record PortfolioOverview(List<BuildingSummary> Buildings, List<StaffAccess> Staff, List<UnallocatedExpense> Expenses);
public record AssignedRepair(int Id, string Building, string Unit, string Description, bool Complete, bool Started);
