namespace Common.Enums;

/// <summary>How a category is routed (spec 3.5).</summary>
public enum RoutingType : byte
{
    /// <summary>Always routed to the category's fixed handling department.</summary>
    FixedDepartment = 1,

    /// <summary>Routed to the HR handler of the requester's department.</summary>
    RequesterDepartmentHr = 2
}
