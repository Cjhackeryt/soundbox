using System.Collections.Concurrent;
using MacroDeck.Sdk.Widgets;

namespace SoundBox.Tests;

/// <summary>
/// Stands in for the host widget API and records every appearance request, so the per-widget
/// countdown can be asserted without a live Macro Deck host.
/// </summary>
internal sealed class RecordingWidgetApi : IWidgetApi
{
	private readonly ConcurrentQueue<(string WidgetId, string? Label, IReadOnlyCollection<WidgetAppearanceProperty> Clear)> _requests = new();

	public IReadOnlyCollection<(string WidgetId, string? Label, IReadOnlyCollection<WidgetAppearanceProperty> Clear)> Requests => [.. _requests];

	/// <summary>Labels this widget was shown, in order. A cleared label appears as null.</summary>
	public IReadOnlyList<string?> LabelsFor(string widgetId) =>
		[.. _requests.Where(r => r.WidgetId == widgetId).Select(r => r.Label)];

	/// <summary>True when this widget's label override was cleared back to its configured text.</summary>
	public bool WasRestored(string widgetId) =>
		_requests.Any(r => r.WidgetId == widgetId && r.Clear.Contains(WidgetAppearanceProperty.Label));

	public int UpdateCountFor(string widgetId) => _requests.Count(r => r.WidgetId == widgetId && r.Label is not null);

	public IReadOnlyList<WidgetTargetInfo> GetWidgets() => [];

	public bool Exists(string widgetId) => true;

	public Task<bool> ApplyAsync(WidgetAppearanceRequest request, CancellationToken cancellationToken = default)
	{
		_requests.Enqueue((request.WidgetId, request.Patch.Label, request.ClearProperties));
		return Task.FromResult(true);
	}
}
