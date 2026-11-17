using System.Text.Json;
using System.Text.Json.Serialization;
using Common.DTOs.Ai.Evaluation;

namespace Services.Implementations.Ai.Evaluation;

/// <summary>Reads the evaluation set file. Strict: an unknown field is an error, so a typo is not silently ignored.</summary>
public static class AiEvaluationSetLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };

    public static async Task<AiEvaluationSet> LoadAsync(string path, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<AiEvaluationSet>(stream, JsonOptions, cancellationToken)
               ?? throw new InvalidDataException("The evaluation set file is empty.");
    }
}
