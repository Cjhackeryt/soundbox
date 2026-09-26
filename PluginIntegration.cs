using MacroDeck.Sdk;
using MacroDeck.Sdk.Actions;
using MacroDeck.Sdk.Variables;
using Serilog;
using SoundBox.Actions;
using SoundBox.Audio;
using SoundBox.Variables;
using SoundBox.Widgets;

namespace SoundBox;

public sealed class PluginIntegration : IPluginIntegration, IVariableProvider, IDisposable
{
    private readonly AudioManager _audioManager;
    private readonly PlaybackCountdownService _countdown;
    private readonly PlaybackRemainingVariableProvider _variables;

    public PluginIntegration(ILogger logger)
    {
        _audioManager = new AudioManager(logger);
        _countdown = new PlaybackCountdownService(_audioManager, logger);
        _variables = new PlaybackRemainingVariableProvider(_audioManager);
        Actions =
        [
            new PlaySoundAction(_audioManager, _countdown, logger),
            new StopSoundAction(_audioManager, _countdown, logger)
        ];
    }

    public IReadOnlyList<IActionDefinition> Actions { get; }

    public IReadOnlyList<VariableDefinition> Variables => _variables.Variables;

    public ValueTask<VariableReading> ReadAsync(string localId, CancellationToken cancellationToken = default) =>
        _variables.ReadAsync(localId, cancellationToken);

    public Task InitializeAsync(IIntegrationContext context)
    {
        // Runs again after a reconnect, so re-attaching replaces the stale host API.
        _countdown.Attach(context.Widgets);
        return Task.CompletedTask;
    }

    public Task ShutdownAsync()
    {
        Dispose();
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _countdown.Dispose();
        _audioManager.Dispose();
    }
}
