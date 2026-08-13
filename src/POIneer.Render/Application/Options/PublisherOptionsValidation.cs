namespace POIneer.Render.Application.Options;

public static class PublisherOptionsValidation
{
    public const string RequiredDestinationDirMessage = "Publisher:DestinationDir must be set";

    public static bool HasRequiredDestinationDir(PublisherOptions options)
        => !string.IsNullOrWhiteSpace(options.DestinationDir);
}
