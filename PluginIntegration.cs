using MacroDeck.Sdk;
using MacroDeck.Sdk.Actions;
using Serilog;
using SoundBox.Actions;
using SoundBox.Audio;

namespace SoundBox;

public sealed class PluginIntegration : IPluginIntegration, IDisposable
{
    private readonly AudioManager _audioManager;

    public PluginIntegration(ILogger logger)
    {
        _audioManager = new AudioManager(logger);
        Actions =
        [
            new PlaySoundAction(_audioManager, logger),
            new StopSoundAction(_audioManager, logger)
        ];
    }

    public IReadOnlyList<IActionDefinition> Actions { get; }

    public Task InitializeAsync(IIntegrationContext context) => Task.CompletedTask;

    public Task ShutdownAsync()
    {
        _audioManager.Dispose();
        return Task.CompletedTask;
    }

    public void Dispose() => _audioManager.Dispose();
}
