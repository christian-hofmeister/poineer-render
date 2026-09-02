namespace POIneer.Render.Application.Contracts;

// Governs what an IDatasetPublisher does when its destination already contains a file for
// the same region and version.
public enum DatasetPublishOverwritePolicy
{
    // Leave the existing file in place and report the publish as skipped.
    Skip,

    // Leave the existing file in place only when it already matches the source bytes;
    // otherwise replace it with the current source artifact.
    SkipIfIdentical,

    // Replace the existing file with the new one.
    Overwrite,

    // Throw instead of silently keeping or replacing the existing file.
    Fail
}
