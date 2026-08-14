using POIneer.Render.Application.Contracts;

namespace POIneer.Render.Application.Ports;

public interface IDatasetPublisher
{
    // Publishes an already-validated, already-canonical dataset artifact to the configured
    // publish destination (see PublisherOptions). Distinct from the in-place promotion that
    // RenderRegion performs when moving a validated staging file to its canonical outDir
    // location - this is the separate, externally-configured destination described by
    // issue #132 and the surrounding dataset-publishing epic (#133-#137).
    Task<DatasetPublishResult> PublishAsync(
        DatasetPublishRequest request,
        CancellationToken cancellationToken = default);
}
