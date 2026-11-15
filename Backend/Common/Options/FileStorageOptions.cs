namespace Common.Options;

/// <summary>Where attachments are stored. Bound from the "Attachments" configuration section.</summary>
public sealed class FileStorageOptions
{
    public const string SectionName = "Attachments";

    /// <summary>Relative to the API's content root (or absolute). Must not be under wwwroot; App_Data is git-ignored.</summary>
    public string Directory { get; set; } = "App_Data/attachments";
}
