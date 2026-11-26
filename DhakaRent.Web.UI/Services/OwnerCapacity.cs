using System.Data;
using DhakaRent.DataAccess.Context;
using DhakaRent.Entities.Models;
using Microsoft.EntityFrameworkCore;

namespace DhakaRent.Web.UI.Services;

public class OwnerCapacity(ApplicationDbContext db)
{
    public async Task CreateAsync(string ownerId, bool building, Func<Task> create)
    {
        await using var transaction = db.Database.IsRelational()
            ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable) : null;
        var plan = await db.Set<OwnerSubscription>().SingleOrDefaultAsync(s => s.OwnerId == ownerId);
        if (plan != null)
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(6));
            var period = new DateOnly(today.Year, today.Month, 1);
            if (plan.MonthlyFee > 0 && !(plan.TrialThrough >= today) &&
                !await db.Set<SubscriptionMonth>().AnyAsync(m => m.OwnerId == ownerId && m.Month == period && m.ConfirmedUtc != null))
                throw new ArgumentException("Your subscription payment for this month must be confirmed to add inventory.");
            var count = building
                ? await db.Buildings.CountAsync(b => b.OwnerId == ownerId && !b.IsArchived)
                : await db.RentalUnits.CountAsync(u => u.Floor.Building.OwnerId == ownerId && !u.Floor.Building.IsArchived);
            if (count >= (building ? plan.BuildingLimit : plan.UnitLimit))
                throw new ArgumentException(building ? "Your plan's building limit has been reached." : "Your plan's unit limit has been reached.");
        }
        await create();
        if (transaction != null) await transaction.CommitAsync();
    }
}
