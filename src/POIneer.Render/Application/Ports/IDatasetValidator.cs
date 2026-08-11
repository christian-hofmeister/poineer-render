using POIneer.Render.Application.Contracts;

namespace POIneer.Render.Application.Ports;

public interface IDatasetValidator
{
    Task<DatasetValidationResult> ValidateAsync(
        string path,
        CancellationToken cancellationToken = default);
}