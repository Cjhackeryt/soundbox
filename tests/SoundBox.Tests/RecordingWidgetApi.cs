using System.Collections.Concurrent;
using System.Text.Json;
using MacroDeck.Sdk.Widgets;

namespace SoundBox.Tests;

/// <summary>
/// Stands in for the host widget API and records every appearance request, so the per-widget
/// countdown can be asserted without a live Macro Deck host.
/// </summary>
internal sealed class RecordingWidgetApi : IWidgetApi
{
	private readonly ConcurrentQueue<(string WidgetId, string? Label, IReadOnlyCollection<WidgetAppearanceProperty> Clear, IReadOnlyCollection<string> StateIds)> _requests = new();
	private readonly ConcurrentDictionary<string, WidgetDefinition> _widgets = new(StringComparer.Ordinal);
	private readonly ConcurrentDictionary<(string WidgetId, string StateId), string> _labels = new();

	public IReadOnlyCollection<(string WidgetId, string? Label, IReadOnlyCollection<WidgetAppearanceProperty> Clear, IReadOnlyCollection<string> StateIds)> Requests => [.. _requests];
	public bool WidgetDataAvailable { get; set; } = true;

	/// <summary>Labels this widget was shown, in order. A cleared label appears as null.</summary>
	public IReadOnlyList<string?> LabelsFor(string widgetId) =>
		[.. _requests.Where(r => r.WidgetId == widgetId).Select(r => r.Label)];

	/// <summary>True when this widget's configured label was restored.</summary>
	public bool WasRestored(string widgetId) =>
		_requests.Any(request =>
			request.WidgetId == widgetId
			&& (request.Clear.Contains(WidgetAppearanceProperty.Label)
				|| request.Label is { } label
					&& _widgets.TryGetValue(widgetId, out var widget)
					&& request.StateIds.Any(stateId =>
						widget.ConfiguredLabels.TryGetValue(stateId, out var configuredLabel)
						&& (string.Equals(configuredLabel, label, StringComparison.Ordinal)
							|| configuredLabel is null && label.Length == 0))));

	public int UpdateCountFor(string widgetId) => _requests.Count(r => r.WidgetId == widgetId && r.Label is not null);

	public void AddWidget(string widgetId, IReadOnlyDictionary<string, string?> configuredLabels, string currentStateId)
	{
		_widgets[widgetId] = new WidgetDefinition(configuredLabels, currentStateId);
		foreach (var (stateId, label) in configuredLabels)
		{
			if (label is not null)
			{
				_labels[(widgetId, stateId)] = label;
			}
		}
	}

	public void SetCurrentState(string widgetId, string stateId)
	{
		if (!_widgets.TryGetValue(widgetId, out var widget))
		{
			throw new ArgumentException("The widget has not been registered.", nameof(widgetId));
		}

		_widgets[widgetId] = widget with { CurrentStateId = stateId };
	}

	public string? EffectiveLabelFor(string widgetId, string stateId)
	{
		return _labels.TryGetValue((widgetId, stateId), out var label) ? label : null;
	}

	public IReadOnlyList<WidgetTargetInfo> GetWidgets() =>
		[.. _widgets.Select(pair => new WidgetTargetInfo
		{
			Id = pair.Key,
			Label = EffectiveLabelFor(pair.Key, pair.Value.CurrentStateId) ?? "Button",
			Location = "Recording widget",
			Type = "ActionButton",
			States = [.. pair.Value.ConfiguredLabels.Select(state => new WidgetStateInfo(state.Key, state.Key))],
			CurrentStateId = pair.Value.CurrentStateId
		})];

	public bool Exists(string widgetId) => true;

	public Task<WidgetDataSnapshot?> GetWidgetDataAsync(string widgetId, CancellationToken cancellationToken = default)
	{
		if (!WidgetDataAvailable || !_widgets.TryGetValue(widgetId, out var widget))
		{
			return Task.FromResult<WidgetDataSnapshot?>(null);
		}

		var states = widget.ConfiguredLabels.Select(state => new
		{
			id = state.Key,
			label = state.Key,
			appearance = new { label = EffectiveLabelFor(widgetId, state.Key) }
		});
		var data = JsonSerializer.SerializeToElement(new
		{
			label = EffectiveLabelFor(widgetId, widget.CurrentStateId) ?? "Button",
			states
		});
		return Task.FromResult<WidgetDataSnapshot?>(new WidgetDataSnapshot(widgetId, "action-button", data));
	}

	public Task<bool> ApplyAsync(WidgetAppearanceRequest request, CancellationToken cancellationToken = default)
	{
		_requests.Enqueue((request.WidgetId, request.Patch.Label, request.ClearProperties, request.ResolveStateIds()));

		if (_widgets.TryGetValue(request.WidgetId, out var widget))
		{
			foreach (var stateId in ResolveStateIds(request, widget))
			{
				if (request.Patch.Label is { } label)
				{
					_labels[(request.WidgetId, stateId)] = label;
				}

				if (request.ClearProperties.Contains(WidgetAppearanceProperty.Label))
				{
					_labels.TryRemove((request.WidgetId, stateId), out _);
				}
			}
		}

		return Task.FromResult(true);
	}

	private static IReadOnlyCollection<string> ResolveStateIds(WidgetAppearanceRequest request, WidgetDefinition widget)
	{
		var stateIds = request.ResolveStateIds();
		if (stateIds.Count == 1 && WidgetStates.IsCurrent(stateIds.First()))
		{
			return [widget.CurrentStateId];
		}

		return stateIds.Count == 1 && WidgetStates.IsAll(stateIds.First())
			? [.. widget.ConfiguredLabels.Keys]
			: stateIds;
	}

	private sealed record WidgetDefinition(
		IReadOnlyDictionary<string, string?> ConfiguredLabels,
		string CurrentStateId);
}
