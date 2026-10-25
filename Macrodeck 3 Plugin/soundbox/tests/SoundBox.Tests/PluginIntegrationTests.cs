using MacroDeck.Localization;
using MacroDeck.Sdk;
using MacroDeck.Sdk.Actions;
using Serilog;
using SoundBox;
using SoundBox.Actions;
using SoundBox.Audio;
using SoundBox.Widgets;
using Xunit;

namespace SoundBox.Tests;

public sealed class PluginIntegrationTests
{
	private readonly ILogger _logger = new LoggerConfiguration().CreateLogger();

	[Fact]
	public void PluginIntegrationRegistersExpectedActions()
	{
		using var integration = new PluginIntegration(_logger);

		Assert.NotNull(integration.Actions);
		Assert.Equal(2, integration.Actions.Count);

		var actionIds = integration.Actions.Select(a => a.Id).ToArray();
		Assert.Contains("play-sound", actionIds);
		Assert.Contains("stop-sound", actionIds);
	}

	[Fact]
	public void PlaySoundActionHasExpectedConfiguration()
	{
		using var audioManager = new AudioManager(_logger);
		using var countdown = new PlaybackCountdownService(audioManager, _logger);
		var action = new PlaySoundAction(audioManager, countdown, _logger);

		Assert.Equal("play-sound", action.Id);
		Assert.Equal(MacroDeckPlatform.Windows, action.Platforms);
		Assert.Equal(6, action.Parameters.Count);

		var parameterNames = action.Parameters.Select(p => p.Name).ToArray();
		Assert.Contains("soundFile", parameterNames);
		Assert.Contains("outputDevice", parameterNames);
		Assert.Contains("monitor", parameterNames);
		Assert.Contains("volume", parameterNames);
		Assert.Contains("loop", parameterNames);
		Assert.Contains("showTimer", parameterNames);

		var timer = action.Parameters.Single(p => p.Name == "showTimer");
		Assert.Equal(true, timer.DefaultValue);

		var secondary = action.Parameters.Single(p => p.Name == "outputDevice");
		Assert.False(secondary.Required);
	}

	[Fact]
	public async Task SoundBoxConfigFlowCompletesWithPrimaryDevice()
	{
		using var audioManager = new AudioManager(_logger);
		var flow = new SoundBox.Config.SoundBoxConfigFlow(audioManager, _logger);

		var started = await flow.StartAsync(new TestConfigFlowContext(), CancellationToken.None);
		Assert.Equal(MacroDeck.Sdk.ConfigFlow.ConfigFlowResultKind.Step, started.Kind);
		Assert.NotNull(started.NextStep);
		Assert.Contains(started.NextStep.Fields, f => f.Name == SoundBox.Config.SoundBoxConfigFlow.PrimaryOutputDeviceField);

		var submitted = await flow.SubmitAsync(
			started.NextStep.StepId,
			new Dictionary<string, object?> { [SoundBox.Config.SoundBoxConfigFlow.PrimaryOutputDeviceField] = "default" },
			new TestConfigFlowContext(),
			CancellationToken.None);
		Assert.Equal(MacroDeck.Sdk.ConfigFlow.ConfigFlowResultKind.Complete, submitted.Kind);
	}

	[Fact]
	public async Task SoundBoxConfigFlowRejectsEmptyPrimaryDevice()
	{
		using var audioManager = new AudioManager(_logger);
		var flow = new SoundBox.Config.SoundBoxConfigFlow(audioManager, _logger);

		var started = await flow.StartAsync(new TestConfigFlowContext(), CancellationToken.None);
		var submitted = await flow.SubmitAsync(
			started.NextStep!.StepId,
			new Dictionary<string, object?> { [SoundBox.Config.SoundBoxConfigFlow.PrimaryOutputDeviceField] = "" },
			new TestConfigFlowContext(),
			CancellationToken.None);
		Assert.Equal(MacroDeck.Sdk.ConfigFlow.ConfigFlowResultKind.Error, submitted.Kind);
	}

	private sealed class TestConfigFlowContext : MacroDeck.Sdk.ConfigFlow.IConfigFlowContext
	{
		public MacroDeck.Sdk.ConfigFlow.IOAuthSession OAuth => throw new NotSupportedException();
	}

	[Fact]
	public async Task SecondaryOutputOptionsAlwaysIncludeDefault()
	{
		using var audioManager = new AudioManager(_logger);
		using var countdown = new PlaybackCountdownService(audioManager, _logger);
		var action = new PlaySoundAction(audioManager, countdown, _logger);

		var result = await action.GetDynamicOptionsAsync(
			new DynamicOptionsContext
			{
				ParameterName = "outputDevice",
				CurrentParameters = new Dictionary<string, object?>()
			},
			CancellationToken.None);

		Assert.NotEmpty(result.Options);
		Assert.Contains(result.Options, o => o.Value == "default");
	}

	[Fact]
	public async Task PlaySoundActionExecuteWithoutFileFails()
	{
		using var audioManager = new AudioManager(_logger);
		using var countdown = new PlaybackCountdownService(audioManager, _logger);
		var action = new PlaySoundAction(audioManager, countdown, _logger);
		var executor = action.CreateExecutor();

		var context = new ActionExecutionContext
		{
			Parameters = new Dictionary<string, object>()
		};

		var result = await executor.ExecuteAsync(context);

		Assert.Equal(ActionResultStatus.Failed, result.Status);
		Assert.Equal(ActionErrorCodes.InvalidParameter, result.ErrorCode);
	}

	[Fact]
	public async Task PlaySoundActionExecuteWithNonExistentFileFails()
	{
		using var audioManager = new AudioManager(_logger);
		using var countdown = new PlaybackCountdownService(audioManager, _logger);
		var action = new PlaySoundAction(audioManager, countdown, _logger);
		var executor = action.CreateExecutor();

		var context = new ActionExecutionContext
		{
			Parameters = new Dictionary<string, object>
			{
				["soundFile"] = "C:\\path\\does_not_exist_xyz123.wav"
			}
		};

		var result = await executor.ExecuteAsync(context);

		Assert.Equal(ActionResultStatus.Failed, result.Status);
		Assert.Equal(ActionErrorCodes.InvalidParameter, result.ErrorCode);
	}

	[Fact]
	public async Task StopSoundActionExecuteSucceeds()
	{
		using var audioManager = new AudioManager(_logger);
		using var countdown = new PlaybackCountdownService(audioManager, _logger);
		var action = new StopSoundAction(audioManager, countdown, _logger);
		var executor = action.CreateExecutor();

		var context = new ActionExecutionContext
		{
			Parameters = new Dictionary<string, object>()
		};

		var result = await executor.ExecuteAsync(context);

		Assert.Equal(ActionResultStatus.Succeeded, result.Status);
	}

	[Fact]
	public void LocalizationCatalogContainsExpectedKeys()
	{
		Assert.NotNull(Strings.LocalizationCatalog);
		Assert.Equal("plugin:com.cjhackeryt.soundbox", Strings.LocalizationCatalog.Scope);

		var keys = Strings.LocalizationCatalog.KeysOf(Strings.LocalizationCatalog.DefaultCulture).ToArray();
		Assert.Contains("Actions.PlaySound.Name", keys);
		Assert.Contains("Actions.StopSound.Name", keys);
	}
}
