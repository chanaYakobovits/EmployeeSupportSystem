using Common.Constants;

namespace Repositories.Data;

/// <summary>
/// The global SQL Server sequence behind employee codes (spec 4.2). NEXT VALUE FOR is atomic, so two admins creating
/// users at the same moment always get different numbers; the unique index on EmployeeCode remains a safety net.
/// </summary>
public static class EmployeeCodeSequence
{
    public const string Name = "EmployeeCodeNumbers";

    /// <summary>
    /// Moves the sequence past the highest existing "EMP-#####" code. Used by the migration that creates the sequence
    /// (existing databases) and by the seeder (after inserting the demo users with fixed codes).
    /// </summary>
    public static readonly string RestartAfterExistingCodesSql = $"""
        DECLARE @next bigint = ISNULL((
            SELECT MAX(TRY_CAST(SUBSTRING([EmployeeCode], {EmployeeCodeFormat.Prefix.Length + 1}, 20) AS bigint))
            FROM [Users]
            WHERE [EmployeeCode] LIKE '{EmployeeCodeFormat.Prefix}%'), 0) + 1;
        DECLARE @sql nvarchar(200) = N'ALTER SEQUENCE [{Name}] RESTART WITH ' + CAST(@next AS nvarchar(20));
        EXEC sp_executesql @sql;
        """;
}
