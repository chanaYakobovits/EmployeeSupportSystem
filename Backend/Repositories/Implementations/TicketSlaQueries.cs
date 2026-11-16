using System.Linq.Expressions;
using Common.Constants;
using Common.Models;

namespace Repositories.Implementations;

/// <summary>SLA conditions as query expressions – one definition for the filters and the projected columns.</summary>
internal static class TicketSlaQueries
{
    /// <summary>
    /// Past its due time while the SLA clock runs (<see cref="TicketStatuses.Open"/>): WaitingForEmployee is paused, and
    /// resolved, closed or cancelled tickets are done. Served by the filtered index IX_Tickets_DueAt_Open (spec 8.4).
    /// </summary>
    public static Expression<Func<Ticket, bool>> IsOverdue(DateTime now)
    {
        var open = TicketStatuses.Open;
        return t => t.DueAt != null && t.DueAt < now && open.Contains(t.Status);
    }

    /// <summary>
    /// Replaces the parameter of <paramref name="part"/> with <paramref name="ticket"/>, to inline the expression in a
    /// projection over the same ticket (EF translates the result as if it had been written there).
    /// </summary>
    public static Expression Inline<T>(Expression<Func<Ticket, T>> part, ParameterExpression ticket) =>
        new ParameterReplacer(part.Parameters[0], ticket).Visit(part.Body);

    /// <summary>
    /// <paramref name="projection"/> written with a placeholder <c>isOverdue</c> parameter, completed with
    /// <see cref="IsOverdue"/>: (t, isOverdue) => new Dto(..., isOverdue, ...) becomes t => new Dto(..., [IsOverdue(t)], ...).
    /// </summary>
    public static Expression<Func<Ticket, TDto>> WithIsOverdue<TDto>(
        Expression<Func<Ticket, bool, TDto>> projection, DateTime now)
    {
        var ticket = projection.Parameters[0];
        var body = new ParameterReplacer(projection.Parameters[1], Inline(IsOverdue(now), ticket)).Visit(projection.Body);
        return Expression.Lambda<Func<Ticket, TDto>>(body, ticket);
    }

    private sealed class ParameterReplacer(ParameterExpression from, Expression to) : ExpressionVisitor
    {
        protected override Expression VisitParameter(ParameterExpression node) => node == from ? to : node;
    }
}
