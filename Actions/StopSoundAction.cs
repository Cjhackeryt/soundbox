using System.Runtime.InteropServices;
using MacroDeck.Localization;
using MacroDeck.Sdk;
using MacroDeck.Sdk.Actions;
using Serilog;
using SoundBox.Audio;
using SoundBox.Widgets;

namespace SoundBox.Actions;

public sealed class StopSoundAction : IActionDefinition
{
	private readonly AudioManager _audioManager;
	private readonly PlaybackCountdownService _countdown;
	private readonly ILogger _logger;

	public StopSoundAction(AudioManager audioManager, PlaybackCountdownService countdown, ILogger logger)
	{
		_audioManager = audioManager;
		_countdown = countdown;
		_logger = logger.ForContext<StopSoundAction>();
	}

	public string Id => "stop-sound";
	public LocalizedText Name => Strings.Actions.StopSound.Name();
	public LocalizedText Description => Strings.Actions.StopSound.Description();
	public MacroDeckPlatform Platforms => MacroDeckPlatform.Windows;
	public IReadOnlyList<ActionParameter> Parameters => [];
	public IActionExecutor CreateExecutor() => new Executor(_audioManager, _countdown, _logger);

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

			try
			{
				_audioManager.Stop();

				// Restore the configured appearance of any widget whose countdown was running,
				// so a stopped sound never leaves a widget reading 00:00.
				await _countdown.ResetAllAsync(context.CancellationToken);

				return ActionResult.Success();
			}
			catch (Exception exception) when (exception is IOException or InvalidOperationException or COMException)
			{
				_logger.Error(exception, "Unable to stop SoundBox playback.");
				return ActionResult.Failed(ActionErrorCodes.Unavailable, Strings.Actions.StopSound.Errors.StopFailed());
			}
		}
	}
}
