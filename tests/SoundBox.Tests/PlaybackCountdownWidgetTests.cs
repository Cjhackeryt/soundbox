using MacroDeck.Sdk.Actions;
using Serilog;
using SoundBox.Actions;
using SoundBox.Audio;
using SoundBox.Widgets;
using Xunit;
using Xunit.Abstractions;

namespace SoundBox.Tests;

/// <summary>
/// Exercises the per-widget countdown through the real Play Sound and Stop Sound actions,
/// against a real Windows output device, recording the appearance each widget receives.
/// Methods in one class run sequentially.
/// </summary>
public sealed class PlaybackCountdownWidgetTests
{
	private readonly ILogger _logger = new LoggerConfiguration().CreateLogger();
	private readonly ITestOutputHelper _output;

	public PlaybackCountdownWidgetTests(ITestOutputHelper output)
	{
		_output = output;
	}

	[RequiresAudioDeviceFact]
	public async Task PressedWidgetCountsDownAndReturnsToItsConfiguredState()
	{
		var path = CreateWave("widget-5s.wav", TimeSpan.FromSeconds(5));

		using var audioManager = new AudioManager(_logger);
		using var countdown = new PlaybackCountdownService(audioManager, _logger);
		var widgets = new RecordingWidgetApi();
		countdown.Attach(widgets);

		var result = await PlayAsync(audioManager, countdown, path, "widget-airhorn");
		Assert.Equal(ActionResultStatus.Succeeded, result.Status);

		// The countdown belongs to the widget that was pressed, from the very first moment.
		Assert.Equal("00:05", widgets.LabelsFor("widget-airhorn")[0]);

		await WaitUntilAsync(() => widgets.LabelsFor("widget-airhorn").Contains("00:00") || widgets.WasRestored("widget-airhorn"), TimeSpan.FromSeconds(9));

		var labels = widgets.LabelsFor("widget-airhorn").Where(l => l is not null).Select(l => l!).ToArray();
		foreach (var label in labels)
		{
			_output.WriteLine(label);
		}

		// Counts down second by second on that widget only.
		Assert.Contains("00:04", labels);
		Assert.Contains("00:03", labels);
		Assert.Contains("00:02", labels);
		Assert.Contains("00:01", labels);

		// The widget is restored to its configured text rather than left on 00:00.
		Assert.True(widgets.WasRestored("widget-airhorn"), "The widget was never restored.");
		Assert.DoesNotContain("00:00", labels);
	}

	[RequiresAudioDeviceFact]
	public async Task OtherWidgetsAreUntouchedWhileOneWidgetCountsDown()
	{
		var path = CreateWave("widget-6s.wav", TimeSpan.FromSeconds(6));

		using var audioManager = new AudioManager(_logger);
		using var countdown = new PlaybackCountdownService(audioManager, _logger);
		var widgets = new RecordingWidgetApi();
		countdown.Attach(widgets);

		await PlayAsync(audioManager, countdown, path, "button-1");
		await Task.Delay(1200);

		// Buttons 2 and 3 were never pressed, so they must not have been touched at all.
		Assert.DoesNotContain(widgets.Requests, r => r.WidgetId is "button-2" or "button-3");
		Assert.Empty(widgets.LabelsFor("button-2"));
		Assert.Empty(widgets.LabelsFor("button-3"));

		_output.WriteLine($"button-1 saw {widgets.UpdateCountFor("button-1")} updates while 2 and 3 were untouched.");
		Assert.True(widgets.UpdateCountFor("button-1") > 0);

		await countdown.ResetAllAsync(CancellationToken.None);
	}

	[RequiresAudioDeviceFact]
	public async Task EachWidgetShowsItsOwnSoundsDuration()
	{
		var airhorn = CreateWave("own-5s.wav", TimeSpan.FromSeconds(5));
		var applause = CreateWave("own-10s.wav", TimeSpan.FromSeconds(10));

		using var audioManager = new AudioManager(_logger);
		using var countdown = new PlaybackCountdownService(audioManager, _logger);
		var widgets = new RecordingWidgetApi();
		countdown.Attach(widgets);

		// Each widget's first label must reflect the sound configured on that widget.
		await PlayAsync(audioManager, countdown, airhorn, "widget-airhorn");
		Assert.Equal("00:05", widgets.LabelsFor("widget-airhorn")[0]);

		await PlayAsync(audioManager, countdown, applause, "widget-applause");
		Assert.Equal("00:10", widgets.LabelsFor("widget-applause")[0]);

		_output.WriteLine($"airhorn: {string.Join(" ", widgets.LabelsFor("widget-airhorn"))}");
		_output.WriteLine($"applause: {string.Join(" ", widgets.LabelsFor("widget-applause"))}");

		// The first widget's own duration is never written to the second widget.
		Assert.DoesNotContain("00:05", widgets.LabelsFor("widget-applause").Where(l => l is not null).Select(l => l!));

		await countdown.ResetAllAsync(CancellationToken.None);
	}

	[RequiresAudioDeviceFact]
	public async Task StopSoundRestoresTheWidgetInsteadOfLeavingItOnZero()
	{
		var path = CreateWave("stop-widget-20s.wav", TimeSpan.FromSeconds(20));

		using var audioManager = new AudioManager(_logger);
		using var countdown = new PlaybackCountdownService(audioManager, _logger);
		var widgets = new RecordingWidgetApi();
		countdown.Attach(widgets);

		await PlayAsync(audioManager, countdown, path, "widget-airhorn");
		await Task.Delay(1000);

		Assert.Equal("00:19", widgets.LabelsFor("widget-airhorn")[^1]);
		Assert.False(widgets.WasRestored("widget-airhorn"));

		var stopped = await StopAsync(audioManager, countdown);
		Assert.Equal(ActionResultStatus.Succeeded, stopped.Status);

		// Stopped immediately, and restored rather than parked on 00:00.
		Assert.True(widgets.WasRestored("widget-airhorn"), "Stop Sound did not restore the widget.");
		Assert.DoesNotContain("00:00", widgets.LabelsFor("widget-airhorn").Where(l => l is not null).Select(l => l!));

		// Nothing keeps writing to it afterwards.
		var countAfterStop = widgets.Requests.Count;
		await Task.Delay(800);
		Assert.Equal(countAfterStop, widgets.Requests.Count);
	}

	[RequiresAudioDeviceFact]
	public async Task ReplayingTheSameWidgetRestartsOnlyThatWidgetsCountdown()
	{
		var path = CreateWave("replay-widget-10s.wav", TimeSpan.FromSeconds(10));

		using var audioManager = new AudioManager(_logger);
		using var countdown = new PlaybackCountdownService(audioManager, _logger);
		var widgets = new RecordingWidgetApi();
		countdown.Attach(widgets);

		await PlayAsync(audioManager, countdown, path, "widget-airhorn");
		await Task.Delay(1000);
		Assert.Equal("00:09", widgets.LabelsFor("widget-airhorn")[^1]);

		// Pressing the same widget again restarts that widget's countdown.
		await PlayAsync(audioManager, countdown, path, "widget-airhorn");
		Assert.Equal("00:10", widgets.LabelsFor("widget-airhorn")[^1]);

		_output.WriteLine($"airhorn: {string.Join(" ", widgets.LabelsFor("widget-airhorn"))}");

		await countdown.ResetAllAsync(CancellationToken.None);
	}

	[RequiresAudioDeviceFact]
	public async Task LoopingWidgetKeepsCountingAndWrapsAtEachRestart()
	{
		var path = CreateWave("loop-widget-3s.wav", TimeSpan.FromSeconds(3));

		using var audioManager = new AudioManager(_logger);
		using var countdown = new PlaybackCountdownService(audioManager, _logger);
		var widgets = new RecordingWidgetApi();
		countdown.Attach(widgets);

		await PlayAsync(audioManager, countdown, path, "widget-loop", loop: true);
		await Task.Delay(9000);

		var labels = widgets.LabelsFor("widget-loop").Where(l => l is not null).Select(l => l!).ToArray();
		foreach (var label in labels)
		{
			_output.WriteLine(label);
		}

		// Wrapped back to the full duration instead of running down to nothing.
		Assert.True(labels.Count(l => l == "00:03") >= 2, "The countdown did not follow the loop restarts.");
		Assert.Contains("00:01", labels);

		// A looping sound is still playing, so the widget must NOT have been restored.
		Assert.False(widgets.WasRestored("widget-loop"), "A looping widget was restored while still playing.");

		await countdown.ResetAllAsync(CancellationToken.None);
	}

	[RequiresAudioDeviceFact]
	public async Task PressingASecondWidgetRestoresTheFirst()
	{
		var first = CreateWave("seq-3s.wav", TimeSpan.FromSeconds(3));
		var second = CreateWave("seq-8s.wav", TimeSpan.FromSeconds(8));

		using var audioManager = new AudioManager(_logger);
		using var countdown = new PlaybackCountdownService(audioManager, _logger);
		var widgets = new RecordingWidgetApi();
		countdown.Attach(widgets);

		await PlayAsync(audioManager, countdown, first, "widget-1");
		await Task.Delay(600);
		Assert.False(widgets.WasRestored("widget-1"));

		// Pressing widget 2 replaces the playback, so widget 1 goes back to normal.
		await PlayAsync(audioManager, countdown, second, "widget-2");
		await WaitUntilAsync(() => widgets.WasRestored("widget-1"), TimeSpan.FromSeconds(3));

		Assert.True(widgets.WasRestored("widget-1"), "The first widget was not restored when the second was pressed.");
		Assert.Equal("00:08", widgets.LabelsFor("widget-2")[0]);

		await countdown.ResetAllAsync(CancellationToken.None);
	}

	[Fact]
	public async Task DisposingTheServiceStopsAllUpdatesAndCleansUp()
	{
		using var audioManager = new AudioManager(_logger);
		var countdown = new PlaybackCountdownService(audioManager, _logger);
		var widgets = new RecordingWidgetApi();
		countdown.Attach(widgets);

		// No playback running, so the session ends at once; this checks the dispose path
		// leaves nothing able to write to a widget.
		await countdown.StartAsync("widget-x", CancellationToken.None);
		countdown.Dispose();

		var after = widgets.Requests.Count;
		await Task.Delay(600);
		Assert.Equal(after, widgets.Requests.Count);
	}

	private static async Task<ActionResult> PlayAsync(AudioManager audioManager, PlaybackCountdownService countdown, string path, string widgetId, bool loop = false)
	{
		var action = new PlaySoundAction(audioManager, countdown, new LoggerConfiguration().CreateLogger());
		return await action.CreateExecutor().ExecuteAsync(new ActionExecutionContext
		{
			OwnerWidgetId = widgetId,
			Parameters = new Dictionary<string, object>
			{
				["soundFile"] = path,
				["outputDevice"] = "default",
				["monitor"] = false,
				["volume"] = 0,
				["loop"] = loop
			}
		});
	}

	private static async Task<ActionResult> StopAsync(AudioManager audioManager, PlaybackCountdownService countdown)
	{
		var action = new StopSoundAction(audioManager, countdown, new LoggerConfiguration().CreateLogger());
		return await action.CreateExecutor().ExecuteAsync(new ActionExecutionContext
		{
			Parameters = new Dictionary<string, object>()
		});
	}

	private static async Task WaitUntilAsync(Func<bool> condition, TimeSpan timeout)
	{
		var deadline = DateTime.UtcNow + timeout;
		while (DateTime.UtcNow < deadline)
		{
			if (condition())
			{
				return;
			}

			await Task.Delay(50);
		}
	}

	private static string CreateWave(string name, TimeSpan duration)
	{
		var path = Path.Combine(Path.GetTempPath(), $"soundbox-tests-{Guid.NewGuid():N}-{name}");
		TestWave.Create(path, duration);
		return path;
	}
}
