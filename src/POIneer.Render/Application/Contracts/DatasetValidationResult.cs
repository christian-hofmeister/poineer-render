namespace POIneer.Render.Application.Contracts;

public sealed record DatasetValidationResult(
    bool IsValid,
    IReadOnlyList<string> Errors);