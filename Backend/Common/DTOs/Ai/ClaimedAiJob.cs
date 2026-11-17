namespace Common.DTOs.Ai;

/// <summary>A job claimed by the AI worker: ids only – the worker then enters the job's organization.</summary>
public sealed record ClaimedAiJob(int JobId, int OrganizationId);
