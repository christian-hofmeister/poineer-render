using FluentAssertions;
using NSubstitute;
using POIneer.Render.Application.Contracts;
using POIneer.Render.Application.Options;
using POIneer.Render.Application.Ports;
using POIneer.Render.Application.UseCases;
using Xunit;

namespace POIneer.Render.UnitTests.Application.UseCases;

public sealed class RegionUpdateScheduleTests
{
    private readonly IRegionScheduleStateStore _store = Substitute.For<IRegionScheduleStateStore>();
    private readonly DateTimeOffset _now = DateTimeOffset.Parse("2026-01-01T00:00:00Z");
    private readonly RegionDto _region = new("berlin", "Berlin", "https://example.com/berlin.pbf", null);

    private RegionUpdateSchedule CreateSut()
        => new(_store, new RegionUpdateIntervalPolicy(Microsoft.Extensions.Options.Options.Create(new RendererOptions
        {
            WorkDir = "work",
            OutDir = "out",
            RegionsJson = "regions.json"
        })), new FixedTimeProvider(_now));

    [Fact]
    public void IsDue_ReturnsTrue_WithoutPreviousSuccessfulCheck()
        => CreateSut().IsDue(null).Should().BeTrue();

    [Theory]
    [InlineData(-1, false, true)]
    [InlineData(0, false, true)]
    [InlineData(1, false, false)]
    [InlineData(1, true, true)]
    public void IsDue_UsesUtcBoundary_AndAlwaysRetriesPendingProcessing(int daysUntilDue, bool pending, bool due)
    {
        var state = new RegionScheduleState(_region.Id, _region.PbfUrl,
            _now.AddDays(-7), _now.AddDays(daysUntilDue), pending);

        CreateSut().IsDue(state).Should().Be(due);
    }

    [Theory]
    [InlineData("hamburg", "https://example.com/berlin.pbf")]
    [InlineData("berlin", "https://example.com/new.pbf")]
    public async Task ReadAsync_DiscardsStateForDifferentRegionOrSource(string regionId, string url)
    {
        _store.ReadAsync("schedule.json", Arg.Any<CancellationToken>()).Returns(
            new RegionScheduleState(regionId, url, _now, _now.AddDays(7), false));

        (await CreateSut().ReadAsync(_region, "schedule.json", CancellationToken.None)).Should().BeNull();
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
