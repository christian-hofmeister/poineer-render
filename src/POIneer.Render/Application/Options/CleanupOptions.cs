using POIneer.Render.Application.Contracts;

namespace POIneer.Render.Application.Options;

public sealed class CleanupOptions
{
    public const string SectionName = "Cleanup";

    public bool DeleteLocalFilesAfterVerifiedPublish { get; init; }

    public bool IsEnabledFor(DatasetPublisherTarget target)
        => DeleteLocalFilesAfterVerifiedPublish && target == DatasetPublisherTarget.AzureBlob;
}
