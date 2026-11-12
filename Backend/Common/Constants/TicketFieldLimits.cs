namespace Common.Constants;

/// <summary>Field limits of a ticket (spec 3.1, 7.4). Used by the validators and the database configuration.</summary>
public static class TicketFieldLimits
{
    public const int SubjectMaxLength = 150;
    public const int DescriptionMinLength = 10;
    public const int DescriptionMaxLength = 4000;

    /// <summary>Length of the description excerpt shown instead of an empty subject in the requester's list.</summary>
    public const int ExcerptLength = 80;
}
