using System.Runtime.InteropServices;
using MacroDeck.Localization;
using MacroDeck.Sdk.Actions;
using MacroDeck.Sdk.ConfigFlow;
using Serilog;
using SoundBox.Audio;

namespace SoundBox.Config;

public sealed class SoundBoxConfigFlow : IConfigFlow
{
	public const string PrimaryOutputDeviceField = "primaryOutputDevice";
	public const string DefaultDeviceId = "default";

	private const string StepId = "primary-output";

	private readonly AudioManager _audioManager;
	private readonly ILogger _logger;

	public SoundBoxConfigFlow(AudioManager audioManager, ILogger logger)
	{
		_audioManager = audioManager;
		_logger = logger.ForContext<SoundBoxConfigFlow>();
	}

	public Task<ConfigFlowResult> StartAsync(IConfigFlowContext context, CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();
		return Task.FromResult(ConfigFlowResult.Step(BuildStep()));
	}

	public Task<ConfigFlowResult> SubmitAsync(
		string stepId,
		IReadOnlyDictionary<string, object?> input,
		IConfigFlowContext context,
		CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();

		if (!string.Equals(stepId, StepId, StringComparison.Ordinal))
		{
			return Task.FromResult(ConfigFlowResult.Error(BuildStep(), Strings.Config.PrimaryOutput.Errors.UnknownStep()));
		}

		var raw = input.TryGetValue(PrimaryOutputDeviceField, out var value) ? value?.ToString() : null;
		if (string.IsNullOrWhiteSpace(raw))
		{
			var required = MacroDeckStrings.Validation.Required(Strings.Config.PrimaryOutput.Device.Label());
			return Task.FromResult(ConfigFlowResult.Error(
				BuildStep(),
				required,
				new Dictionary<string, LocalizedText> { [PrimaryOutputDeviceField] = required }));
		}

		return Task.FromResult(ConfigFlowResult.Complete("SoundBox"));
	}

	private ConfigFlowStep BuildStep() => new()
	{
		StepId = StepId,
		Title = Strings.Config.PrimaryOutput.Title(),
		Description = Strings.Config.PrimaryOutput.Description(),
		Fields =
		[
			ActionParameter.Choice(
				PrimaryOutputDeviceField,
				BuildDeviceOptions(),
				Strings.Config.PrimaryOutput.Device.Label(),
				Strings.Config.PrimaryOutput.Device.Description(),
				DefaultDeviceId,
				required: true)
		]
	};

	private List<ActionParameterOption> BuildDeviceOptions()
	{
		var options = new List<ActionParameterOption>
		{
			new() { Value = DefaultDeviceId, Label = Strings.Config.PrimaryOutput.Device.DefaultOption() }
		};

		try
		{
			options.AddRange(_audioManager.GetOutputDevices()
				.Select(device => new ActionParameterOption { Value = device.Id, Label = device.Name }));
		}
		catch (Exception exception)
		{
			// The default option above always stays, so the step is never left
			// without a selectable device even when enumeration fails.
			_logger.Warning(exception, "Unable to enumerate Windows audio output devices for the config flow.");
		}

		return options;
	}
}
