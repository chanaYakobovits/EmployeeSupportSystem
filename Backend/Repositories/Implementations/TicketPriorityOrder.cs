using System.Linq.Expressions;
using Common.Constants;
using Common.Enums;
using Common.Models;

namespace Repositories.Implementations;

/// <summary>
/// sortBy=priority of the staff lists (<see cref="Common.DTOs.Tickets.StaffTicketSortFields.Priority"/>), entirely in
/// the ORDER BY of the query (CASE expressions). Every key below the group is a constant outside the group it orders,
/// so it never mixes the order of another group.
/// </summary>
internal static class TicketPriorityOrder
{
    /// <summary>Waiting for the handler = the SLA clock runs (<see cref="TicketStatuses.Open"/>).</summary>
    private static readonly Expression<Func<Ticket, bool>> WaitsForHandler = StatusIn(TicketStatuses.Open);

    /// <summary>1 waits for the handler, 2 for the employee, 3 resolved (waits for confirmation), 4 closed or cancelled.</summary>
    private static readonly Expression<Func<Ticket, int>> Group = Combine<int>(WaitsForHandler, t => 1,
        t => t.Status == TicketStatus.WaitingForEmployee ? 2 : t.Status == TicketStatus.Resolved ? 3 : 4);

    // Within group 1 – constants elsewhere.
    private static readonly Expression<Func<Ticket, byte>> Severity =
        Combine<byte>(WaitsForHandler, t => (byte)(t.Urgency ?? Urgency.Medium), t => 0);
    private static readonly Expression<Func<Ticket, int>> NoDueAtLast =
        Combine<int>(WaitsForHandler, t => t.DueAt == null ? 1 : 0, t => 0);
    private static readonly Expression<Func<Ticket, DateTime?>> DueAt =
        Combine<DateTime?>(WaitsForHandler, t => t.DueAt, t => null);

    // Groups 2–4 – a constant in group 1.
    private static readonly Expression<Func<Ticket, DateTime?>> UpdatedAt =
        Combine<DateTime?>(WaitsForHandler, t => null, t => t.UpdatedAt);

    public static IOrderedQueryable<Ticket> OrderByPriority(this IQueryable<Ticket> tickets, DateTime now) =>
        tickets
            .OrderBy(Group)
            // IsOverdue is false outside group 1 (its statuses are the same TicketStatuses.Open).
            .ThenBy(TicketSlaQueries.WithIsOverdue<int>((t, isOverdue) => isOverdue ? 0 : 1, now))
            .ThenByDescending(Severity)
            .ThenBy(NoDueAtLast)
            .ThenBy(DueAt)
            .ThenByDescending(UpdatedAt)
            .ThenBy(t => t.Id);

    /// <summary>t.Status = a OR t.Status = b … with literals (no collection parameter in every CASE).</summary>
    private static Expression<Func<Ticket, bool>> StatusIn(IEnumerable<TicketStatus> statuses)
    {
        var t = Expression.Parameter(typeof(Ticket), "t");
        var status = Expression.Convert(Expression.Property(t, nameof(Ticket.Status)), typeof(int));
        var body = statuses
            .Select(s => (Expression)Expression.Equal(status, Expression.Constant((int)s)))
            .Aggregate(Expression.OrElse);
        return Expression.Lambda<Func<Ticket, bool>>(body, t);
    }

    /// <summary>t => condition(t) ? whenTrue(t) : otherwise(t), over one parameter.</summary>
    private static Expression<Func<Ticket, T>> Combine<T>(Expression<Func<Ticket, bool>> condition,
        Expression<Func<Ticket, T>> whenTrue, Expression<Func<Ticket, T>> otherwise)
    {
        var t = Expression.Parameter(typeof(Ticket), "t");
        var body = Expression.Condition(
            TicketSlaQueries.Inline(condition, t),
            TicketSlaQueries.Inline(whenTrue, t),
            TicketSlaQueries.Inline(otherwise, t));
        return Expression.Lambda<Func<Ticket, T>>(body, t);
    }
}
