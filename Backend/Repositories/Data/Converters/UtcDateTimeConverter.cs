using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Repositories.Data.Converters;

/// <summary>
/// Stores DateTime values as-is (the application always writes UTC) and marks values
/// read from the database as DateTimeKind.Utc, so they serialize with a "Z" suffix.
/// </summary>
public sealed class UtcDateTimeConverter() : ValueConverter<DateTime, DateTime>(
    value => value.Kind == DateTimeKind.Local ? value.ToUniversalTime() : value,
    value => DateTime.SpecifyKind(value, DateTimeKind.Utc));
