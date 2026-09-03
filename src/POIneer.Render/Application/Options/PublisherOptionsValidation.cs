using POIneer.Render.Application.Contracts;

namespace POIneer.Render.Application.Options;

public static class PublisherOptionsValidation
{
    public const string RequiredDestinationDirMessage = "Publisher:DestinationDir must be set when Publisher:Target is Local";

    public static bool HasRequiredDestinationDir(PublisherOptions options)
        => options.Target != DatasetPublisherTarget.Local || !string.IsNullOrWhiteSpace(options.DestinationDir);
}
