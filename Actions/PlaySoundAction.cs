using System.Runtime.InteropServices;
using MacroDeck.Localization;
using MacroDeck.Sdk;
using MacroDeck.Sdk.Actions;
using Serilog;
using SoundBox.Audio;

namespace SoundBox.Actions;

public sealed class PlaySoundAction : IDynamicOptionsActionDefinition
{
    private const string SoundFile = "soundFile";
    private const string OutputDevice = "outputDevice";
    private const string Monitor = "monitor";
    private const string Volume = "volume";
    private const string Loop = "loop";
    private readonly AudioManager _audioManager;
    private readonly ILogger _logger;

    public PlaySoundAction(AudioManager audioManager, ILogger logger)
    {
        _audioManager = audioManager;
        _logger = logger.ForContext<PlaySoundAction>();
    }

    public string Id => "play-sound";
    public LocalizedText Name => "Play Sound";
    public LocalizedText Description => "Plays a sound through a selected Windows audio output.";
    public MacroDeckPlatform Platforms => MacroDeckPlatform.Windows;

    public IReadOnlyList<ActionParameter> Parameters =>
    [
        ActionParameter.File(SoundFile, "Sound File", "WAV and MP3 files are supported.", ["wav", "mp3"], true),
        ActionParameter.DynamicChoice(OutputDevice, "Output Device", "The Windows output device to receive the sound.", required: true),
        ActionParameter.Toggle(Monitor, "Monitor Sound", "Also play through the default Windows playback device.", true),
        ActionParameter.Slider(Volume, 0, 100, "Volume", "Playback volume.", 1, 100),
        ActionParameter.Toggle(Loop, "Loop", "Restart the sound automatically when it ends.")
    ];

    public IActionExecutor CreateExecutor() => new Executor(_audioManager, _logger);

    public Task<DynamicOptionsResult> GetDynamicOptionsAsync(DynamicOptionsContext context, CancellationToken cancellationToken)
    {
        if (!string.Equals(context.ParameterName, OutputDevice, StringComparison.Ordinal))
        {
            return Task.FromResult(new DynamicOptionsResult { Options = [] });
        }

        try
        {
            var options = new List<ActionParameterOption>
            {
                new() { Value = "default", Label = "Windows Default Playback Device" }
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
                Error = "Windows audio output devices are unavailable."
            });
        }
    }

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
            var rawPath = GetString(context, SoundFile);
            if (string.IsNullOrWhiteSpace(rawPath))
            {
                return Task.FromResult(ActionResult.Failed(ActionErrorCodes.InvalidParameter, "A sound file is required."));
            }

            var filePath = rawPath.Trim('"', ' ');
            if (!File.Exists(filePath))
            {
                return Task.FromResult(ActionResult.Failed(ActionErrorCodes.InvalidParameter, $"Sound file does not exist: {filePath}"));
            }

            var outputDevice = GetString(context, OutputDevice);
            var monitor = GetBool(context, Monitor, true);
            var volume = GetInt(context, Volume, 100);
            var loop = GetBool(context, Loop);

            try
            {
                return _audioManager.Play(filePath, outputDevice, monitor, volume, loop)
                    ? ActionResult.SucceededTask
                    : Task.FromResult(ActionResult.Failed(ActionErrorCodes.Unavailable, "No usable audio output device was found."));
            }
            catch (Exception exception) when (exception is IOException or InvalidOperationException or ArgumentException or NotSupportedException or COMException)
            {
                _logger.Error(exception, "Sound playback failed for {FilePath}", filePath);
                return Task.FromResult(ActionResult.Failed(ActionErrorCodes.Unavailable, "The sound could not be played."));
            }
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
