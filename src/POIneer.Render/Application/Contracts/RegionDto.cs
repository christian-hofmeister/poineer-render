namespace POIneer.Render.Application.Contracts;

// Data Transfer Object for Regions
public sealed record RegionDto(
    string Id,
    string Name,
    string PbfUrl,
    string? Poly);