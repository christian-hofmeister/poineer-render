using POIneer.Render.Application.Contracts;

namespace POIneer.Render.Application.Options;

public static class PublisherOptionsValidation
{
    public const string RequiredDestinationDirMessage = "Publisher:DestinationDir must be set when Publisher:Target is Local";
    public const string ImplementedTargetMessage = "Publisher:Target must be Local until the Azure Blob publisher is implemented";

    public static bool HasRequiredDestinationDir(PublisherOptions options)
        => options.Target != DatasetPublisherTarget.Local || !string.IsNullOrWhiteSpace(options.DestinationDir);

    public static bool HasImplementedTarget(PublisherOptions options)
        => options.Target == DatasetPublisherTarget.Local;
}
