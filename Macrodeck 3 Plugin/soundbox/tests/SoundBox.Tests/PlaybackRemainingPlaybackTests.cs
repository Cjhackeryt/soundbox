using System.Diagnostics;
using System.Globalization;
using Serilog;
using SoundBox.Audio;
using SoundBox.Variables;
using Xunit;
using Xunit.Abstractions;

namespace SoundBox.Tests;

/// <summary>
/// Plays real audio through a real Windows output device and reads the variable the
/// Macro Deck host would read, so the countdown is verified against actual playback
/// rather than a simulated clock. Methods in one class run sequentially.
/// </summary>
public sealed class PlaybackRemainingPlaybackTests
{
	private const string LocalId = "soundbox-playback-remaining";

	private readonly ILogger _logger = new LoggerConfiguration().CreateLogger();
	private readonly ITestOutputHelper _output;

	public PlaybackRemainingPlaybackTests(ITestOutputHelper output)
	{
		_output = output;
	}

	[RequiresAudioDeviceFact]
	public async Task CountdownTracksPlaybackPositionAndEndsAtZero()
	{
		var path = CreateWave("countdown-5s.wav", TimeSpan.FromSeconds(5));

		using var audioManager = new AudioManager(_logger);
		var provider = new PlaybackRemainingVariableProvider(audioManager);

		Assert.True(audioManager.Play(path, "default", monitor: false, volumePercent: 0, loop: false));

		var samples = new List<(double Seconds, string Value)>();
		var started = Stopwatch.StartNew();

		while (started.Elapsed < TimeSpan.FromSeconds(6))
		{
			samples.Add((started.Elapsed.TotalSeconds, await ReadAsync(provider)));
			await Task.Delay(250);
		}

		audioManager.Stop();

		foreach (var sample in samples)
		{
			_output.WriteLine($"{sample.Seconds,5:0.00}s -> {sample.Value}");
		}

		// Starts at the full length of the clip.
		Assert.Equal("00:05", samples[0].Value);

		// Counts down second by second, tracking the clip as it plays.
		Assert.Contains("00:04", samples.Select(s => s.Value));
		Assert.Contains("00:03", samples.Select(s => s.Value));
		Assert.Contains("00:02", samples.Select(s => s.Value));
		Assert.Contains("00:01", samples.Select(s => s.Value));

		// Reaches zero once the sound ends and stays there.
		Assert.Contains("00:00", samples.Select(s => s.Value));
		var firstZero = samples.FindIndex(s => s.Value == "00:00");
		Assert.True(firstZero > 0, "The countdown never reached 00:00.");
		Assert.All(samples.Skip(firstZero), s => Assert.Equal("00:00", s.Value));
	}

	[RequiresAudioDeviceFact]
	public async Task StopResetsTheCountdownImmediately()
	{
		var path = CreateWave("stop-20s.wav", TimeSpan.FromSeconds(20));

		using var audioManager = new AudioManager(_logger);
		var provider = new PlaybackRemainingVariableProvider(audioManager);

		Assert.True(audioManager.Play(path, "default", monitor: false, volumePercent: 0, loop: false));
		await Task.Delay(1000);

		var during = await ReadAsync(provider);
		_output.WriteLine($"1s into a 20s clip: {during}");
		Assert.Equal("00:19", during);

		audioManager.Stop();

		Assert.Equal("00:00", await ReadAsync(provider));
		Assert.False(audioManager.TryGetRemainingPlaybackTime(out var stopped));
		Assert.Equal(TimeSpan.Zero, stopped);
	}

	[RequiresAudioDeviceFact]
	public async Task ReplayingTheSameSoundRestartsTheCountdown()
	{
		var path = CreateWave("restart-10s.wav", TimeSpan.FromSeconds(10));

		using var audioManager = new AudioManager(_logger);
		var provider = new PlaybackRemainingVariableProvider(audioManager);

		Assert.True(audioManager.Play(path, "default", monitor: false, volumePercent: 0, loop: false));
		Assert.Equal("00:10", await ReadAsync(provider));

		await Task.Delay(1000);
		var before = await ReadAsync(provider);
		_output.WriteLine($"1s into the first play: {before}");
		Assert.Equal("00:09", before);

		Assert.True(audioManager.Play(path, "default", monitor: false, volumePercent: 0, loop: false));

		var after = await ReadAsync(provider);
		_output.WriteLine($"immediately after replaying: {after}");
		Assert.Equal("00:10", after);

		audioManager.Stop();
	}

	[RequiresAudioDeviceFact]
	public async Task LoopingSoundWrapsTheCountdownBackToTheFullDuration()
	{
		var path = CreateWave("loop-3s.wav", TimeSpan.FromSeconds(3));

		using var audioManager = new AudioManager(_logger);
		var provider = new PlaybackRemainingVariableProvider(audioManager);

		Assert.True(audioManager.Play(path, "default", monitor: false, volumePercent: 0, loop: true));

		var samples = new List<(double Seconds, string Value)>();
		var started = Stopwatch.StartNew();

		while (started.Elapsed < TimeSpan.FromSeconds(9))
		{
			samples.Add((started.Elapsed.TotalSeconds, await ReadAsync(provider)));
			await Task.Delay(250);
		}

		audioManager.Stop();

		foreach (var sample in samples)
		{
			_output.WriteLine($"{sample.Seconds,5:0.00}s -> {sample.Value}");
		}

		var remaining = new List<int>(samples.Select(s => ParseSeconds(s.Value)));
		var wraps = 0;
		for (var index = 1; index < remaining.Count; index++)
		{
			if (remaining[index] > remaining[index - 1] + 1)
			{
				wraps++;
			}
		}

		_output.WriteLine($"observed {wraps} wrap(s) over {samples.Count} samples");

		// A 3s clip looped for 9s must wrap at least twice, proving the countdown follows
		// the loop position instead of sticking at 00:00.
		Assert.True(wraps >= 2, "The countdown did not follow the loop restarts.");

		// 00:00 may appear for a single sample at a loop boundary, but never beyond that.
		for (var index = 1; index < remaining.Count; index++)
		{
			Assert.True(
				!(remaining[index] == 0 && remaining[index - 1] == 0),
				$"The countdown stuck at 00:00 across samples {index - 1} and {index}.");
		}
	}

	[RequiresAudioDeviceFact]
	public async Task RepeatedPlayAndStopNeverLeavesAStaleCountdown()
	{
		var path = CreateWave("churn-4s.wav", TimeSpan.FromSeconds(4));

		using var audioManager = new AudioManager(_logger);
		var provider = new PlaybackRemainingVariableProvider(audioManager);

		// Interleaves reads with start and stop so a position read can land while the
		// session it belongs to is being torn down.
		for (var round = 0; round < 12; round++)
		{
			Assert.True(audioManager.Play(path, "default", monitor: false, volumePercent: 0, loop: false));
			var started = await ReadAsync(provider);
			Assert.Equal("00:04", started);

			await Task.Delay(120);
			var reading = await ReadAsync(provider);
			Assert.False(reading.Length == 0, "The countdown returned an empty value.");

			audioManager.Stop();
			Assert.Equal("00:00", await ReadAsync(provider));
		}

		_output.WriteLine("12 play/stop rounds completed with no stale countdown.");
	}

	private static string CreateWave(string name, TimeSpan duration)
	{
		var path = Path.Combine(Path.GetTempPath(), $"soundbox-tests-{Guid.NewGuid():N}-{name}");
		TestWave.Create(path, duration);
		return path;
	}

	private static async Task<string> ReadAsync(PlaybackRemainingVariableProvider provider)
	{
		var reading = await provider.ReadAsync(LocalId);
		return Assert.IsType<string>(reading.Value);
	}

	private static int ParseSeconds(string value)
	{
		var parts = value.Split(':');
		return parts.Length switch
		{
			2 => Parse(parts[0]) * 60 + Parse(parts[1]),
			3 => Parse(parts[0]) * 3600 + Parse(parts[1]) * 60 + Parse(parts[2]),
			_ => 0
		};

		static int Parse(string part) => int.Parse(part, CultureInfo.InvariantCulture);
	}
}
