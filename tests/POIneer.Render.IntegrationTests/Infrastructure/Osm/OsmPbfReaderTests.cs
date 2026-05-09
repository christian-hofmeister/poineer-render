using FluentAssertions;
using POIneer.Render.Infrastructure.Adapters.Osm;
using POIneer.Render.Application.Ports.Model;
using Xunit;

namespace POIneer.Render.IntegrationTests.Infrastructure.Osm;

public sealed class OsmPbfReaderTests
{
    private const string GoerlitzerParkFixtureName = "berlin-goerlitzer-park-mini.osm.pbf";

    [Fact]
    public async Task ReadAmenityNodesAsync_reads_amenity_nodes_from_goerlitzer_park_fixture()
    {
        // Arrange
        var fixturePath = GetFixturePath(GoerlitzerParkFixtureName);
        var sut = new OsmPbfReader();

        // Act
        var pois = await ReadAllAsync(sut.ReadAmenityNodesAsync(fixturePath));

        // Assert
        pois.Should().NotBeEmpty("the fixture should contain at least one amenity node");
        pois.Should().OnlyContain(poi => poi.OsmId > 0);
        pois.Should().OnlyContain(poi => !string.IsNullOrWhiteSpace(poi.Amenity));
        pois.Should().OnlyContain(poi => poi.Tags.ContainsKey("amenity"));
        pois.Should().OnlyContain(poi => poi.Tags["amenity"] == poi.Amenity);
        pois.Select(poi => poi.OsmId).Should().OnlyHaveUniqueItems();

        pois.Should().OnlyContain(poi =>
            poi.Latitude >= 52.48 &&
            poi.Latitude <= 52.51 &&
            poi.Longitude >= 13.42 &&
            poi.Longitude <= 13.46,
            "the fixture is a small Görlitzer Park extract in Berlin");
    }

    [Fact]
    public async Task ReadAmenityNodesAsync_preserves_optional_name_and_tags_from_fixture()
    {
        // Arrange
        var fixturePath = GetFixturePath(GoerlitzerParkFixtureName);
        var sut = new OsmPbfReader();

        // Act
        var pois = await ReadAllAsync(sut.ReadAmenityNodesAsync(fixturePath));

        // Assert
        pois.Should().Contain(poi => !string.IsNullOrWhiteSpace(poi.Name));

        var namedPois = pois.Where(poi => poi.Name is not null).ToList();
        namedPois.Should().OnlyContain(poi => poi.Tags.ContainsKey("name"));
        namedPois.Should().OnlyContain(poi => poi.Tags["name"] == poi.Name);
    }

    [Fact]
    public async Task ReadAmenityNodesAsync_throws_when_file_does_not_exist()
    {
        // Arrange
        var sut = new OsmPbfReader();
        var missingPath = Path.Combine(AppContext.BaseDirectory, "TestData", "missing.osm.pbf");

        // Act
        var act = () => ReadAllAsync(sut.ReadAmenityNodesAsync(missingPath));

        // Assert
        await act.Should().ThrowAsync<FileNotFoundException>();
    }

    [Fact]
    public async Task ReadAmenityNodesAsync_throws_when_file_extension_is_not_pbf()
    {
        // Arrange
        var sut = new OsmPbfReader();
        var invalidPath = Path.Combine(AppContext.BaseDirectory, "TestData", "not-a-pbf.txt");
        await File.WriteAllTextAsync(invalidPath, "not a pbf");

        // Act
        var act = () => ReadAllAsync(sut.ReadAmenityNodesAsync(invalidPath));

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Expected a .pbf file*");
    }

    private static string GetFixturePath(string fileName)
    {
        var fixturePath = Path.Combine(AppContext.BaseDirectory, "TestData", fileName);

        File.Exists(fixturePath).Should().BeTrue(
            "the integration test project copies TestData/*.pbf to the output directory");

        return fixturePath;
    }

    private static async Task<IReadOnlyList<RawPoi>> ReadAllAsync(IAsyncEnumerable<RawPoi> source)
    {
        var result = new List<RawPoi>();

        await foreach (var item in source)
        {
            result.Add(item);
        }

        return result;
    }
}
