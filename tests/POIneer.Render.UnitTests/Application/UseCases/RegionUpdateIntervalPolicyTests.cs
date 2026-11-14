using FluentAssertions;
using Microsoft.Extensions.Configuration;
using POIneer.Render.Application.Contracts;
using POIneer.Render.Application.Options;
using POIneer.Render.Application.UseCases;
using Xunit;

namespace POIneer.Render.UnitTests.Application.UseCases;

public sealed class RegionUpdateIntervalPolicyTests
{
    [Theory]
    [InlineData(null, null, 7)]
    [InlineData("14", null, 14)]
    [InlineData(null, 1, 1)]
    [InlineData("14", 3, 3)]
    public void GetIntervalDays_UsesRegionOverrideOrConfiguredDefault(
        string? configuredDefault, int? regionOverride, int expectedDays)
    {
        var values = new Dictionary<string, string?>
        {
            ["Renderer:WorkDir"] = "work",
            ["Renderer:OutDir"] = "out",
            ["Renderer:RegionsJson"] = "regions.json"
        };
        if (configuredDefault is not null)
            values["Renderer:DefaultUpdateIntervalDays"] = configuredDefault;
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        var options = configuration.GetSection("Renderer").Get<RendererOptions>()!;
        var sut = new RegionUpdateIntervalPolicy(Microsoft.Extensions.Options.Options.Create(options));
        var region = new RegionDto("berlin", "Berlin", "https://example.com/berlin.pbf", null,
            UpdateIntervalDays: regionOverride);

        sut.GetIntervalDays(region).Should().Be(expectedDays);
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(-1, false)]
    [InlineData(1, true)]
    [InlineData(7, true)]
    public void DefaultIntervalValidation_RequiresPositiveDays(int days, bool expectedValid)
    {
        var options = new RendererOptions
        {
            WorkDir = "work",
            OutDir = "out",
            RegionsJson = "regions.json",
            DefaultUpdateIntervalDays = days
        };

        RendererOptionsValidation.HasValidUpdateInterval(options).Should().Be(expectedValid);
    }
}
