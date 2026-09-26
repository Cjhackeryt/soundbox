using System.Runtime.InteropServices;
using MacroDeck.Localization;
using MacroDeck.Sdk;
using MacroDeck.Sdk.Actions;
using Serilog;
using SoundBox.Audio;
using SoundBox.Widgets;

namespace SoundBox.Actions;

public sealed class PlaySoundAction : IDynamicOptionsActionDefinition
{
    private const string SoundFile = "soundFile";
    private const string OutputDevice = "outputDevice";
    private const string Monitor = "monitor";
    private const string Volume = "volume";
    private const string Loop = "loop";
    private readonly AudioManager _audioManager;
    private readonly PlaybackCountdownService _countdown;
    private readonly ILogger _logger;

    public PlaySoundAction(AudioManager audioManager, PlaybackCountdownService countdown, ILogger logger)
    {
        _audioManager = audioManager;
        _countdown = countdown;
        _logger = logger.ForContext<PlaySoundAction>();
    }

	public string Id => "play-sound";
	public LocalizedText Name => Strings.Actions.PlaySound.Name();
	public LocalizedText Description => Strings.Actions.PlaySound.Description();
	public MacroDeckPlatform Platforms => MacroDeckPlatform.Windows;

	public IReadOnlyList<ActionParameter> Parameters =>
	[
		ActionParameter.File(SoundFile, Strings.Actions.PlaySound.SoundFile.Label(), Strings.Actions.PlaySound.SoundFile.Description(), ["wav", "mp3"], true),
		ActionParameter.DynamicChoice(OutputDevice, Strings.Actions.PlaySound.OutputDevice.Label(), Strings.Actions.PlaySound.OutputDevice.Description(), required: true),
		ActionParameter.Toggle(Monitor, Strings.Actions.PlaySound.Monitor.Label(), Strings.Actions.PlaySound.Monitor.Description(), true),
		ActionParameter.Slider(Volume, 0, 100, Strings.Actions.PlaySound.Volume.Label(), Strings.Actions.PlaySound.Volume.Description(), 1, 100),
		ActionParameter.Toggle(Loop, Strings.Actions.PlaySound.Loop.Label(), Strings.Actions.PlaySound.Loop.Description())
	];

	public IActionExecutor CreateExecutor() => new Executor(_audioManager, _countdown, _logger);

	public Task<DynamicOptionsResult> GetDynamicOptionsAsync(DynamicOptionsContext context, CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();

		if (!string.Equals(context.ParameterName, OutputDevice, StringComparison.Ordinal))
		{
			return Task.FromResult(new DynamicOptionsResult { Options = [] });
		}

		try
		{
			var options = new List<ActionParameterOption>
			{
				new() { Value = "default", Label = Strings.Actions.PlaySound.OutputDevice.DefaultOption() }
			};

			options.AddRange(
				_audioManager.GetOutputDevices()
					.Select(device => new ActionParameterOption { Value = device.Id, Label = device.Name })
			);

			return Task.FromResult(new DynamicOptionsResult { Options = [.. options], CacheSeconds = 5 });
		}
		catch (COMException exception)
		{
			_logger.Warning(exception, "Unable to enumerate Windows audio output devices.");
			return Task.FromResult(new DynamicOptionsResult
			{
				Options = [],
				Error = Strings.Actions.PlaySound.OutputDevice.Unavailable()
			});
		}
	}

	private sealed class Executor : IActionExecutor
	{
		private readonly AudioManager _audioManager;
		private readonly PlaybackCountdownService _countdown;
		private readonly ILogger _logger;

		public Executor(AudioManager audioManager, PlaybackCountdownService countdown, ILogger logger)
		{
			_audioManager = audioManager;
			_countdown = countdown;
			_logger = logger;
		}

		public async Task<ActionResult> ExecuteAsync(ActionExecutionContext context)
		{
			context.CancellationToken.ThrowIfCancellationRequested();

			var rawPath = GetString(context, SoundFile);
			if (string.IsNullOrWhiteSpace(rawPath))
			{
				return ActionResult.Failed(ActionErrorCodes.InvalidParameter, Strings.Actions.PlaySound.Errors.SoundFileRequired());
			}

			var filePath = rawPath.Trim('"', ' ');
			if (!File.Exists(filePath))
			{
				return ActionResult.Failed(ActionErrorCodes.InvalidParameter, Strings.Actions.PlaySound.Errors.SoundFileNotFound(filePath));
			}

			var outputDevice = GetString(context, OutputDevice);
			var monitor = GetBool(context, Monitor, true);
			var volume = GetInt(context, Volume, 100);
			var loop = GetBool(context, Loop);

			try
			{
				if (!_audioManager.Play(filePath, outputDevice, monitor, volume, loop))
				{
					return ActionResult.Failed(ActionErrorCodes.Unavailable, Strings.Actions.PlaySound.Errors.NoDeviceFound());
				}
			}
			catch (Exception exception) when (exception is IOException or InvalidOperationException or ArgumentException or NotSupportedException or COMException)
			{
				_logger.Error(exception, "Sound playback failed for {FilePath}", filePath);
				return ActionResult.Failed(ActionErrorCodes.Unavailable, Strings.Actions.PlaySound.Errors.PlaybackFailed());
			}

			// The countdown belongs to the widget that was pressed, so it is driven by this
			// action instance's own widget rather than by a shared variable.
			if (context.OwnerWidgetId is { Length: > 0 } ownerWidgetId)
			{
				await _countdown.StartAsync(ownerWidgetId, context.CancellationToken);
			}

			return ActionResult.Success();
		}

		private static string? GetString(ActionExecutionContext context, string name) =>
			context.Parameters.TryGetValue(name, out var value) ? value?.ToString() : null;

		private static bool GetBool(ActionExecutionContext context, string name, bool defaultValue = false) =>
			context.Parameters.TryGetValue(name, out var value) && value is not null
				? Convert.ToBoolean(value, System.Globalization.CultureInfo.InvariantCulture)
				: defaultValue;

		private static int GetInt(ActionExecutionContext context, string name, int defaultValue) =>
			context.Parameters.TryGetValue(name, out var value) && value is not null
				? Convert.ToInt32(value, System.Globalization.CultureInfo.InvariantCulture)
				: defaultValue;
	}
}
