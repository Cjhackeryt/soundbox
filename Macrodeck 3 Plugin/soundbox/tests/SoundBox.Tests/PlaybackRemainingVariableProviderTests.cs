using Serilog;
using SoundBox.Audio;
using SoundBox.Playback;
using SoundBox.Variables;
using Xunit;

namespace SoundBox.Tests;

public sealed class PlaybackRemainingVariableProviderTests
{
	private readonly ILogger _logger = new LoggerConfiguration().CreateLogger();

	[Fact]
	public void DeclaresThePlaybackRemainingTextVariable()
	{
		using var audioManager = new AudioManager(_logger);
		var provider = new PlaybackRemainingVariableProvider(audioManager);

		var definition = Assert.Single(provider.Variables);

		Assert.Equal("soundbox_playback_remaining", definition.Name);
		Assert.Equal("soundbox-playback-remaining", definition.ResolvedId);
		Assert.Equal(MacroDeck.Sdk.Variables.VariableType.Text, definition.Type);
		Assert.Equal(MacroDeck.Sdk.Variables.VariableMaterialization.Eager, definition.Materialization);
		Assert.NotNull(definition.RefreshInterval);
		Assert.InRange(definition.RefreshInterval.Value, TimeSpan.FromMilliseconds(250), TimeSpan.FromMilliseconds(500));
	}

	[Fact]
	public async Task ReadsZeroWhenNothingIsPlaying()
	{
		using var audioManager = new AudioManager(_logger);
		var provider = new PlaybackRemainingVariableProvider(audioManager);

		var reading = await provider.ReadAsync("soundbox-playback-remaining");

		Assert.Equal("00:00", reading.Value);
	}

	[Fact]
	public async Task UnknownVariableIdReadsUnavailable()
	{
		using var audioManager = new AudioManager(_logger);
		var provider = new PlaybackRemainingVariableProvider(audioManager);

		var reading = await provider.ReadAsync("not-a-soundbox-variable");

		Assert.Same(MacroDeck.Sdk.Variables.VariableReading.Unavailable, reading);
	}

	[Theory]
	[InlineData(0, "00:00")]
	[InlineData(-5, "00:00")]
	[InlineData(1, "00:01")]
	[InlineData(5, "00:05")]
	[InlineData(9, "00:09")]
	[InlineData(10, "00:10")]
	[InlineData(59, "00:59")]
	[InlineData(60, "01:00")]
	[InlineData(65, "01:05")]
	[InlineData(90, "01:30")]
	[InlineData(600, "10:00")]
	[InlineData(3599, "59:59")]
	[InlineData(3600, "01:00:00")]
	[InlineData(3932, "01:05:32")]
	[InlineData(36000, "10:00:00")]
	public void FormatsRemainingAsMinutesAndHours(int seconds, string expected)
	{
		Assert.Equal(expected, RemainingTimeFormatter.Format(TimeSpan.FromSeconds(seconds)));
	}

	[Theory]
	[InlineData(0.001, "00:01")]
	[InlineData(0.4, "00:01")]
	[InlineData(0.9, "00:01")]
	[InlineData(1.0, "00:01")]
	[InlineData(1.2, "00:02")]
	public void RoundsUpSoTheLastSecondIsStillVisible(double seconds, string expected)
	{
		Assert.Equal(expected, RemainingTimeFormatter.Format(TimeSpan.FromSeconds(seconds)));
	}
}
