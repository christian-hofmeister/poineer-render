using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using POIneer.Render.Application.Options;
using POIneer.Render.Application.Ports;
using POIneer.Render.Infrastructure.Process;

namespace POIneer.Render.Infrastructure.VectorTiles;

public sealed class PlanetilerVectorTileGenerator : IVectorTileGenerator
{
    private readonly ILogger<PlanetilerVectorTileGenerator> _logger;
    private readonly VectorTileOptions _options;
    private readonly IProcessRunner _processRunner;

    public PlanetilerVectorTileGenerator(
        IProcessRunner processRunner,
        IOptions<VectorTileOptions> options,
        ILogger<PlanetilerVectorTileGenerator> logger)
    {
        _processRunner = processRunner;
        _options = options.Value;
        _logger = logger;
    }

    public async Task GenerateAsync(
        string pbfPath,
        string outputPath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pbfPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);

        if (_options.Enabled)
        {
            if (string.IsNullOrWhiteSpace(_options.PlanetilerJarPath))
                throw new InvalidOperationException("Vector tile generation is enabled, but no Planetiler JAR path is configured.");

            if (!File.Exists(_options.PlanetilerJarPath))
                throw new FileNotFoundException($"Planetiler JAR not found: {_options.PlanetilerJarPath}", _options.PlanetilerJarPath);
        }

        if (!File.Exists(pbfPath))
            throw new FileNotFoundException($"PBF file not found: {pbfPath}", pbfPath);

        var outputDirectory = Path.GetDirectoryName(Path.GetFullPath(outputPath));
        if (!string.IsNullOrWhiteSpace(outputDirectory))
        {
            Directory.CreateDirectory(outputDirectory);
        }

        var startInfo = BuildStartInfo(pbfPath, outputPath);

        _logger.LogInformation(
            "Generating vector tiles with Planetiler: {PbfPath} -> {OutputPath}",
            pbfPath,
            outputPath);

        var result = await _processRunner.RunAsync(startInfo, cancellationToken);

        LogPlanetilerOutput(result.StandardOutput, result.StandardError);

        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"Planetiler failed (ExitCode={result.ExitCode}). StdErr: {result.StandardError}");
        }

        if (!File.Exists(outputPath))
        {
            throw new FileNotFoundException(
                $"Planetiler completed successfully, but the expected PMTiles output was not created: {outputPath}",
                outputPath);
        }
    }

    private ProcessStartInfo BuildStartInfo(string pbfPath, string outputPath)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = ProcessUtils.ResolveExecutablePath(_options.JavaExecutablePath) ?? _options.JavaExecutablePath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        if (!string.IsNullOrWhiteSpace(_options.JavaMaxHeapSize))
        {
            startInfo.ArgumentList.Add($"-Xmx{_options.JavaMaxHeapSize}");
        }

        startInfo.ArgumentList.Add("-jar");
        startInfo.ArgumentList.Add(_options.PlanetilerJarPath!);
        startInfo.ArgumentList.Add($"--osm-path={Path.GetFullPath(pbfPath)}");
        startInfo.ArgumentList.Add($"--output={Path.GetFullPath(outputPath)}");
        startInfo.ArgumentList.Add("--force");

        if (!string.IsNullOrWhiteSpace(_options.Profile))
        {
            startInfo.ArgumentList.Add($"--profile={_options.Profile}");
        }

        if (_options.MinZoom is { } minZoom)
        {
            startInfo.ArgumentList.Add($"--minzoom={minZoom}");
        }

        if (_options.MaxZoom is { } maxZoom)
        {
            startInfo.ArgumentList.Add($"--maxzoom={maxZoom}");
        }

        foreach (var argument in _options.AdditionalArguments.Where(a => !string.IsNullOrWhiteSpace(a)))
        {
            startInfo.ArgumentList.Add(argument);
        }

        return startInfo;
    }

    private void LogPlanetilerOutput(string standardOutput, string standardError)
    {
        const int maxChars = 8_000;

        if (!string.IsNullOrWhiteSpace(standardOutput))
        {
            var stdout = standardOutput.Length <= maxChars
                ? standardOutput
                : standardOutput[..maxChars] + "…(truncated)";

            _logger.LogDebug("Planetiler stdout: {StdOut}", stdout);
        }

        if (!string.IsNullOrWhiteSpace(standardError))
        {
            var stderr = standardError.Length <= maxChars
                ? standardError
                : standardError[..maxChars] + "…(truncated)";

            _logger.LogWarning("Planetiler stderr: {StdErr}", stderr);
        }
    }
}
