using System.Text.RegularExpressions;

namespace POIneer.Render.Domain.Models;

// Shared parsing/validation for globally unique, hierarchical region identifiers
// (ADR 0007), e.g. "geofabrik/europe/germany/berlin". Renderer paths and blob names
// use the same region ID syntax as the v1 manifest contract.
public static class RegionIdentifier
{
    private const char SegmentSeparator = '/';
    private const string RegionSegmentPattern = "[a-z0-9]+(?:[._-][a-z0-9]+)*";

    // Absolute anchors reject a trailing newline.
    private static readonly Regex RegionIdPattern = new(
        @"\A" + RegionSegmentPattern + "(?:/" + RegionSegmentPattern + @")*\z",
        RegexOptions.NonBacktracking);

    // Validates a single segment that must not itself contain the hierarchy separator -
    // used for values that are not hierarchical, such as a dataset Version.
    public static void ValidateSingleSegment(string value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);

        if (value.Contains(SegmentSeparator))
        {
            throw new ArgumentException(
                $"'{value}' is not a valid {parameterName}: '{SegmentSeparator}' is not allowed here.",
                parameterName);
        }

        ValidateSegmentCharacters(value, value, parameterName);
    }

    // Validates a hierarchical, '/'-separated region id and returns its segments, e.g.
    // "geofabrik/europe/germany/berlin" -> ["geofabrik", "europe", "germany", "berlin"].
    public static string[] ValidateHierarchicalId(string value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);

        if (!RegionIdPattern.IsMatch(value))
        {
            throw new ArgumentException(
                $"'{value}' is not a valid {parameterName}: use slash-separated segments of lowercase ASCII letters and digits, with '.', '-', or '_' only between alphanumeric groups.",
                parameterName);
        }

        return value.Split(SegmentSeparator);
    }

    // Returns the last ("leaf") segment of a hierarchical region id, e.g. "berlin" for
    // "geofabrik/europe/germany/berlin". Published dataset artifacts use the full
    // hierarchical id as their directory/blob-name prefix but the leaf segment as the
    // file's base name, so the file name stays short and readable instead of repeating
    // the whole hierarchy that the surrounding directory/prefix already encodes.
    public static string GetLeafSegment(string regionId)
    {
        var segments = ValidateHierarchicalId(regionId, nameof(regionId));
        return segments[^1];
    }

    // Combines a base directory with every segment of a hierarchical region id,
    // producing one nested directory per segment with OS-native separators - e.g.
    // CombinePath("out", "geofabrik/europe/germany/berlin") on Windows yields
    // "out\geofabrik\europe\germany\berlin" rather than a path with embedded '/'
    // characters passed straight through from the region id.
    public static string CombinePath(string baseDir, string regionId)
    {
        var segments = ValidateHierarchicalId(regionId, nameof(regionId));
        var parts = new string[segments.Length + 1];
        parts[0] = baseDir;
        Array.Copy(segments, 0, parts, 1, segments.Length);
        return Path.Combine(parts);
    }

    // Allow-list rather than a block-list: only ASCII letters, digits, '.', '-', and '_'
    // are accepted within a segment, and a segment must not be exactly "." or "..". This
    // is deliberately stricter than only rejecting path separators - it also rules out
    // platform-specific invalid filename characters (e.g. ':', '<', '>' on Windows)
    // without having to enumerate them, since the same identifiers are used unchanged on
    // Windows dev machines, the Linux VPS, and Azure Blob Storage.
    private static void ValidateSegmentCharacters(string segment, string originalValue, string parameterName)
    {
        if (segment is "." or "..")
        {
            throw new ArgumentException(
                $"'{originalValue}' is not a valid {parameterName}: '.' and '..' segments are not allowed.",
                parameterName);
        }

        foreach (var c in segment)
        {
            if (!char.IsAsciiLetterOrDigit(c) && c is not ('.' or '-' or '_'))
            {
                throw new ArgumentException(
                    $"'{originalValue}' is not a valid {parameterName}: only ASCII letters, digits, '.', '-', and '_' are allowed within each '{SegmentSeparator}'-separated segment, but found '{c}'.",
                    parameterName);
            }
        }
    }
}
