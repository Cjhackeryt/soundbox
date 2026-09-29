namespace SoundBox.Config;

public sealed class PlaybackRouting
{
	private readonly object _sync = new();
	private string _primaryOutputDeviceId = SoundBoxConfigFlow.DefaultDeviceId;

	public string PrimaryOutputDeviceId
	{
		get
		{
			lock (_sync)
			{
				return _primaryOutputDeviceId;
			}
		}
		set
		{
			lock (_sync)
			{
				_primaryOutputDeviceId = string.IsNullOrWhiteSpace(value)
					? SoundBoxConfigFlow.DefaultDeviceId
					: value;
			}
		}
	}
}
