using System.Text.RegularExpressions;
using Common.Exceptions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Repositories.Data;
using Repositories.Interfaces;

namespace Repositories.Implementations;

public sealed partial class UnitOfWork(AppDbContext db) : IUnitOfWork
{
    // SQL Server: 2601 = duplicate key in a unique index, 2627 = unique / primary key constraint.
    private const int DuplicateKeyInUniqueIndex = 2601;
    private const int UniqueConstraintViolation = 2627;

    /// <exception cref="ConflictException">A concurrency token did not match (the row was changed in the meantime).</exception>
    /// <exception cref="UniqueConstraintException">A unique index was violated.</exception>
    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Translated here so the Services layer never depends on EF exception types.
            throw new ConflictException(errorType: "concurrency");
        }
        catch (DbUpdateException exception) when (exception.InnerException is SqlException
                                                   {
                                                       Number: DuplicateKeyInUniqueIndex or UniqueConstraintViolation,
                                                   } sql)
        {
            // Only the index name is kept; the message also contains the duplicate value (e.g. an e-mail).
            var match = IndexNamePattern().Match(sql.Message);
            throw new UniqueConstraintException(match.Success ? match.Groups[1].Value : null);
        }
    }

    public async Task<T> ExecuteInTransactionAsync<T>(Func<CancellationToken, Task<T>> action,
        CancellationToken cancellationToken)
    {
        // Disposing without Commit rolls back (and releases transaction-owned locks).
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var result = await action(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return result;
    }

    [GeneratedRegex("(?:index|constraint) '([^']+)'")]
    private static partial Regex IndexNamePattern();
}
