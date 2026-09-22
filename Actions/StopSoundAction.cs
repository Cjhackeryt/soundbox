using System.Runtime.InteropServices;
using MacroDeck.Localization;
using MacroDeck.Sdk;
using MacroDeck.Sdk.Actions;
using Serilog;
using SoundBox.Audio;

namespace SoundBox.Actions;

public sealed class StopSoundAction : IActionDefinition
{
    private readonly AudioManager _audioManager;
    private readonly ILogger _logger;

    public StopSoundAction(AudioManager audioManager, ILogger logger)
    {
        _audioManager = audioManager;
        _logger = logger.ForContext<StopSoundAction>();
    }

	public string Id => "stop-sound";
	public LocalizedText Name => Strings.Actions.StopSound.Name();
	public LocalizedText Description => Strings.Actions.StopSound.Description();
	public MacroDeckPlatform Platforms => MacroDeckPlatform.Windows;
	public IReadOnlyList<ActionParameter> Parameters => [];
	public IActionExecutor CreateExecutor() => new Executor(_audioManager, _logger);

	private sealed class Executor : IActionExecutor
	{
		private readonly AudioManager _audioManager;
		private readonly ILogger _logger;

		public Executor(AudioManager audioManager, ILogger logger)
		{
			_audioManager = audioManager;
			_logger = logger;
		}

		public Task<ActionResult> ExecuteAsync(ActionExecutionContext context)
		{
			context.CancellationToken.ThrowIfCancellationRequested();

			try
			{
				_audioManager.Stop();
				return ActionResult.SucceededTask;
			}
			catch (Exception exception) when (exception is IOException or InvalidOperationException or COMException)
			{
				_logger.Error(exception, "Unable to stop SoundBox playback.");
				return Task.FromResult(ActionResult.Failed(ActionErrorCodes.Unavailable, Strings.Actions.StopSound.Errors.StopFailed()));
			}
		}
	}
}
