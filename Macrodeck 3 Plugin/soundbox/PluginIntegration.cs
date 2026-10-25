using MacroDeck.Plugin.Hosting.Transport;
using MacroDeck.Sdk;
using MacroDeck.Sdk.Actions;
using MacroDeck.Sdk.ConfigFlow;
using MacroDeck.Sdk.Variables;
using Serilog;
using SoundBox.Actions;
using SoundBox.Audio;
using SoundBox.Config;
using SoundBox.Variables;
using SoundBox.Widgets;

namespace SoundBox;

public sealed class PluginIntegration : IPluginIntegration, IVariableProvider, IConfigFlowProvider, IDisposable
{
    private readonly AudioManager _audioManager;
    private readonly PlaybackCountdownService _countdown;
    private readonly PlaybackRemainingVariableProvider _variables;
    private readonly PlaybackRouting _routing;
    private readonly ILogger _logger;

    public PluginIntegration(ILogger logger)
    {
        _logger = logger.ForContext<PluginIntegration>();
        _audioManager = new AudioManager(logger);
        _countdown = new PlaybackCountdownService(_audioManager, logger);
        _variables = new PlaybackRemainingVariableProvider(_audioManager);
        _routing = new PlaybackRouting();
        Actions =
        [
            new PlaySoundAction(_audioManager, _countdown, _routing, logger),
            new StopSoundAction(_audioManager, _countdown, logger)
        ];
    }

    public IReadOnlyList<IActionDefinition> Actions { get; }

    public IReadOnlyList<VariableDefinition> Variables => _variables.Variables;

    public bool AllowsMultipleConfigurations => false;

    public IConfigFlow CreateConfigFlow() => new SoundBoxConfigFlow(_audioManager, _logger);

    public ValueTask<VariableReading> ReadAsync(string localId, CancellationToken cancellationToken = default) =>
        _variables.ReadAsync(localId, cancellationToken);

    public async Task InitializeAsync(IIntegrationContext context)
    {
        // Runs again after a reconnect, so re-attaching replaces the stale host API.
        _countdown.Attach(context.Widgets);

        try
        {
            var entries = await context.Config.GetEntriesAsync(CancellationToken.None).ConfigureAwait(false);
            if (entries.Count > 0)
            {
                var stored = await context.Config.GetStringAsync(entries[0].Id, SoundBoxConfigFlow.PrimaryOutputDeviceField, CancellationToken.None).ConfigureAwait(false);
                _routing.PrimaryOutputDeviceId = string.IsNullOrWhiteSpace(stored)
                    ? SoundBoxConfigFlow.DefaultDeviceId
                    : stored;
            }
        }
        catch (Exception exception) when (exception is InvalidOperationException or TimeoutException or HostInvocationException or KeyNotFoundException)
        {
            _logger.Warning(exception, "Unable to read the SoundBox primary output device; keeping the previous value.");
        }
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
