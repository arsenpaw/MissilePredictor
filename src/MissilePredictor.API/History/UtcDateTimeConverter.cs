using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace MissilePredictor.API.History;

/// <summary>
/// Stores <see cref="DateTime"/> as UTC and reads it back with <see cref="DateTimeKind.Utc"/>. A value with
/// <see cref="DateTimeKind.Unspecified"/> is taken to be UTC already, not shifted by the server's offset.
/// </summary>
internal sealed class UtcDateTimeConverter() : ValueConverter<DateTime, DateTime>(
    v => v.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(v, DateTimeKind.Utc) : v.ToUniversalTime(),
    v => DateTime.SpecifyKind(v, DateTimeKind.Utc));
