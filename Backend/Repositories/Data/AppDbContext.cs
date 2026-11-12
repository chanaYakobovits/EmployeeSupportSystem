using Common.Interfaces;
using Common.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Repositories.Data.Converters;

namespace Repositories.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options, IOrganizationContext organizationContext)
    : DbContext(options)
{
    /// <summary>
    /// Organization of the signed-in user, or the one a background job entered explicitly; null otherwise
    /// (login, refresh, seeder). Read by the global query filters on every query (EF parameterizes context members
    /// per execution).
    /// </summary>
    private int? CurrentOrganizationId => organizationContext.OrganizationId;

    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<Department> Departments => Set<Department>();
    public DbSet<User> Users => Set<User>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<SlaPolicy> SlaPolicies => Set<SlaPolicy>();
    public DbSet<Ticket> Tickets => Set<Ticket>();
    public DbSet<TicketAiAnalysis> TicketAiAnalyses => Set<TicketAiAnalysis>();
    public DbSet<AiAnalysisJob> AiAnalysisJobs => Set<AiAnalysisJob>();
    public DbSet<TicketStatusHistory> TicketStatusHistory => Set<TicketStatusHistory>();
    public DbSet<TicketComment> TicketComments => Set<TicketComment>();
    public DbSet<TicketAttachment> TicketAttachments => Set<TicketAttachment>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<UserInvitation> UserInvitations => Set<UserInvitation>();
    public DbSet<InvitationEmailJob> InvitationEmailJobs => Set<InvitationEmailJob>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        // Employee codes (spec 4.2): global and atomic. See EmployeeCodeSequence.
        modelBuilder.HasSequence<long>(EmployeeCodeSequence.Name).StartsAt(1).IncrementsBy(1);

        
        foreach (var foreignKey in modelBuilder.Model.GetEntityTypes().SelectMany(entity => entity.GetForeignKeys()))
        {
            foreignKey.DeleteBehavior = DeleteBehavior.Restrict;
        }

        ApplyOrganizationFilters(modelBuilder);
    }

    /// <summary>
    /// Spec 6.2: every entity with an OrganizationId is filtered to the signed-in user's organization.
    /// Fail closed: without a signed-in user no rows are returned. The explicit "!= null" matters for AuditLog,
    /// whose OrganizationId is nullable - EF's C# null semantics would otherwise match null to null.
    /// Bypassed with IgnoreQueryFilters() only where no user can exist yet: login lookup, refresh-token lookup and
    /// invitation-token lookup (set-password). Background jobs do not bypass: they enter an organization explicitly
    /// (ISystemOrganizationContext).
    /// Entities without OrganizationId (UserRole, RefreshToken, UserInvitation, Ticket children, AI jobs) are not
    /// filtered - they are always reached through a filtered parent. InvitationEmailJob has an OrganizationId but is
    /// deliberately not filtered: the worker claims jobs of every organization before it knows which one (it then
    /// enters each job's organization) and the claim returns only ids.
    /// </summary>
    private void ApplyOrganizationFilters(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Organization>()
            .HasQueryFilter(e => CurrentOrganizationId != null && e.Id == CurrentOrganizationId);
        modelBuilder.Entity<Department>()
            .HasQueryFilter(e => CurrentOrganizationId != null && e.OrganizationId == CurrentOrganizationId);
        modelBuilder.Entity<User>()
            .HasQueryFilter(e => CurrentOrganizationId != null && e.OrganizationId == CurrentOrganizationId);
        modelBuilder.Entity<Category>()
            .HasQueryFilter(e => CurrentOrganizationId != null && e.OrganizationId == CurrentOrganizationId);
        modelBuilder.Entity<SlaPolicy>()
            .HasQueryFilter(e => CurrentOrganizationId != null && e.OrganizationId == CurrentOrganizationId);
        modelBuilder.Entity<Ticket>()
            .HasQueryFilter(e => CurrentOrganizationId != null && e.OrganizationId == CurrentOrganizationId);
        modelBuilder.Entity<AuditLog>()
            .HasQueryFilter(e => CurrentOrganizationId != null && e.OrganizationId == CurrentOrganizationId);
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        // Warns that required navigations to filtered entities (e.g. UserRole -> User) may hide rows.
        // Intended: the unfiltered children are only ever reached through their filtered parent.
        optionsBuilder.ConfigureWarnings(w =>
            w.Ignore(CoreEventId.PossibleIncorrectRequiredNavigationWithQueryFilterInteractionWarning));
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        
        configurationBuilder.Properties<DateTime>()
            .HaveColumnType("datetime2")
            .HaveConversion<UtcDateTimeConverter>();

        configurationBuilder.Properties<DateTime?>()
            .HaveColumnType("datetime2")
            .HaveConversion<UtcDateTimeConverter>();
    }
}
