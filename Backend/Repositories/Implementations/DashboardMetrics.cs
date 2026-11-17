using System.Linq.Expressions;
using Common.Constants;
using Common.DTOs.Dashboard;
using Common.Enums;
using Common.Models;
using Microsoft.EntityFrameworkCore;

namespace Repositories.Implementations;

/// <summary>
/// The definitions of the dashboard metrics (spec 7.9) – the only place. Each is an expression over a ticket, so the
/// database computes it (spec 13: GROUP BY, no tickets in memory); <see cref="DashboardRepository"/> combines them into
/// its queries with <see cref="Row{T1,TRow}"/> and never writes a condition of its own.
/// Instants are UTC; calendar days are days in Israel (spec 11.6).
/// </summary>
internal static class DashboardMetrics
{
    /// <summary>
    /// The SQL Server name of Israel's time zone (sys.time_zone_info, the Windows id – also on SQL Server for Linux).
    /// Includes the daylight saving rules, like Asia/Jerusalem in .NET (IsraelTime).
    /// </summary>
    public const string IsraelSqlTimeZone = "Israel Standard Time";

    private static readonly IReadOnlyList<TicketStatus> Unresolved = TicketStatuses.Unresolved;

    /// <summary>
    /// The tickets of a dashboard: the requester's department (view=requesters), the handling department
    /// (view=handling), or the whole organization (the organization query filter applies anyway).
    /// </summary>
    public static Expression<Func<Ticket, bool>> InScope(DashboardScope scope)
    {
        if (scope.DepartmentId is not { } departmentId)
        {
            return t => true;
        }

        return scope.View == DashboardView.Handling
            ? t => t.AssignedDepartmentId == departmentId
            : t => t.RequesterDepartmentId == departmentId;
    }

    /// <summary>
    /// "פניות פתוחות" – the current state, regardless of the range: waiting for the department or for the employee
    /// (<see cref="TicketStatuses.Unresolved"/>). Resolved is not open: it only waits for the employee's confirmation
    /// or the automatic closing.
    /// </summary>
    public static readonly Expression<Func<Ticket, bool>> IsOpen = t => Unresolved.Contains(t.Status);

    /// <summary>
    /// "חריגות SLA פתוחות" – the current state: past the due time while the SLA clock runs. The one definition of the
    /// staff lists (<see cref="TicketSlaQueries.IsOverdue"/>): WaitingForEmployee is paused and never overdue.
    /// </summary>
    public static Expression<Func<Ticket, bool>> IsOverdue(DateTime nowUtc) => TicketSlaQueries.IsOverdue(nowUtc);

    /// <summary>Opened at or after <paramref name="sinceUtc"/>: "חדשות השבוע" (since midnight in Israel 6 days ago).</summary>
    public static Expression<Func<Ticket, bool>> OpenedSince(DateTime sinceUtc) => t => t.CreatedAt >= sinceUtc;

    /// <summary>
    /// Opened in the range – the base of "לפי קטגוריה", "לפי סטטוס", "סווגו אוטומטית" and the opened line of the trend.
    /// </summary>
    public static Expression<Func<Ticket, bool>> OpenedIn(DashboardWindow window)
    {
        var (from, to) = (window.FromUtc, window.ToUtc);
        return t => t.CreatedAt >= from && t.CreatedAt < to;
    }

    /// <summary>
    /// Resolved in the range – the base of "זמן פתרון ממוצע" and "עמידה ב-SLA". Resolved now, or closed after it was
    /// resolved (ResolvedAt is kept when it closes). A reopening clears ResolvedAt (task 3.5), so a reopened ticket
    /// counts only when resolved again, by its last resolution. Cancelled never counts.
    /// </summary>
    public static Expression<Func<Ticket, bool>> ResolvedIn(DashboardWindow window)
    {
        var (from, to) = (window.FromUtc, window.ToUtc);
        return t => t.ResolvedAt != null && t.ResolvedAt >= from && t.ResolvedAt < to
                    && (t.Status == TicketStatus.Resolved || t.Status == TicketStatus.Closed);
    }

    /// <summary>
    /// Closed in the range – the closed line of the trend. ClosedAt is set only by closing (a cancellation does not set
    /// it), and a closed ticket is final.
    /// </summary>
    public static Expression<Func<Ticket, bool>> ClosedIn(DashboardWindow window)
    {
        var (from, to) = (window.FromUtc, window.ToUtc);
        return t => t.Status == TicketStatus.Closed && t.ClosedAt != null && t.ClosedAt >= from && t.ClosedAt < to;
    }

    /// <summary>
    /// "זמן פתרון ממוצע" – seconds from the opening to the (last) resolution, calendar time like the SLA (spec 13.1).
    /// Waiting for the employee is included: it is the employee's experience. DATEDIFF in seconds fits an int for 68 years.
    /// </summary>
    public static readonly Expression<Func<Ticket, int?>> ResolutionSeconds =
        t => EF.Functions.DateDiffSecond(t.CreatedAt, t.ResolvedAt);

    /// <summary>
    /// "עמידה ב-SLA" – resolved by the due time. DueAt already includes the paused time (WaitingForEmployee) and, after a
    /// reopening, the new clock (task 3.5); a ticket without urgency got the Medium hours. No due time – not counted
    /// in the base (<see cref="HasDueAt"/>).
    /// </summary>
    public static readonly Expression<Func<Ticket, bool>> MetSla = t => t.DueAt != null && t.ResolvedAt <= t.DueAt;

    /// <summary>The base of <see cref="MetSla"/>.</summary>
    public static readonly Expression<Func<Ticket, bool>> HasDueAt = t => t.DueAt != null;

    /// <summary>
    /// "סווגו אוטומטית" – classified by the AI. Fallback (the AI failed or was unsure), Manual and not classified yet are not.
    /// </summary>
    public static readonly Expression<Func<Ticket, bool>> AutoClassified = t => t.ClassificationSource == ClassificationSource.Ai;

    /// <summary>The opening day in Israel: CONVERT(date, CreatedAt AT TIME ZONE 'UTC' AT TIME ZONE 'Israel Standard Time').</summary>
    public static readonly Expression<Func<Ticket, DateTime>> OpenedDay = t =>
        EF.Functions.AtTimeZone(EF.Functions.AtTimeZone(t.CreatedAt, "UTC"), IsraelSqlTimeZone).Date;

    /// <summary>The closing day in Israel – only for tickets of <see cref="ClosedIn"/> (ClosedAt is set).</summary>
    public static readonly Expression<Func<Ticket, DateTime>> ClosedDay = t =>
        EF.Functions.AtTimeZone(EF.Functions.AtTimeZone(t.ClosedAt!.Value, "UTC"), IsraelSqlTimeZone).Date;

    // ===================== Combining the definitions into one projection =====================

    /// <summary>
    /// t => shape(t, part1(t)): the metric is inlined into the projection, so EF translates it as if it had been written
    /// there (a CASE in SELECT / GROUP BY). Lets a query compute several metrics in one pass without repeating them.
    /// </summary>
    public static Expression<Func<Ticket, TRow>> Row<T1, TRow>(Expression<Func<Ticket, T1, TRow>> shape,
        Expression<Func<Ticket, T1>> part1) =>
        Inline<TRow>(shape, part1);

    public static Expression<Func<Ticket, TRow>> Row<T1, T2, TRow>(Expression<Func<Ticket, T1, T2, TRow>> shape,
        Expression<Func<Ticket, T1>> part1, Expression<Func<Ticket, T2>> part2) =>
        Inline<TRow>(shape, part1, part2);

    public static Expression<Func<Ticket, TRow>> Row<T1, T2, T3, TRow>(Expression<Func<Ticket, T1, T2, T3, TRow>> shape,
        Expression<Func<Ticket, T1>> part1, Expression<Func<Ticket, T2>> part2, Expression<Func<Ticket, T3>> part3) =>
        Inline<TRow>(shape, part1, part2, part3);

    public static Expression<Func<Ticket, TRow>> Row<T1, T2, T3, T4, TRow>(
        Expression<Func<Ticket, T1, T2, T3, T4, TRow>> shape, Expression<Func<Ticket, T1>> part1,
        Expression<Func<Ticket, T2>> part2, Expression<Func<Ticket, T3>> part3, Expression<Func<Ticket, T4>> part4) =>
        Inline<TRow>(shape, part1, part2, part3, part4);

    /// <summary>left(t) || right(t), over one parameter.</summary>
    public static Expression<Func<Ticket, bool>> Or(Expression<Func<Ticket, bool>> left, Expression<Func<Ticket, bool>> right)
    {
        var ticket = left.Parameters[0];
        return Expression.Lambda<Func<Ticket, bool>>(
            Expression.OrElse(left.Body, new Replacer(right.Parameters[0], ticket).Visit(right.Body)), ticket);
    }

    private static Expression<Func<Ticket, TRow>> Inline<TRow>(LambdaExpression shape, params LambdaExpression[] parts)
    {
        var ticket = shape.Parameters[0];
        var body = shape.Body;
        for (var i = 0; i < parts.Length; i++)
        {
            var part = new Replacer(parts[i].Parameters[0], ticket).Visit(parts[i].Body);
            body = new Replacer(shape.Parameters[i + 1], part).Visit(body);
        }

        return Expression.Lambda<Func<Ticket, TRow>>(body, ticket);
    }

    private sealed class Replacer(ParameterExpression from, Expression to) : ExpressionVisitor
    {
        protected override Expression VisitParameter(ParameterExpression node) => node == from ? to : node;
    }
}
