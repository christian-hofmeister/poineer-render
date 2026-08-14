namespace POIneer.Render.Application.Contracts;

// Outcome of a publish attempt. WasSkipped is true when a file for the same region and
// version already existed at the destination and PublisherOptions.OverwritePolicy is Skip
// - the existing file was left untouched.
public sealed record DatasetPublishResult(
    string DestinationPath,
    bool WasSkipped);
