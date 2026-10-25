using System.Runtime.InteropServices;
using NAudio.CoreAudioApi;
using Xunit;

namespace SoundBox.Tests;

/// <summary>
/// Skips when the machine has no active Windows render endpoint, so the suite stays
/// runnable on agents and build machines without audio hardware.
/// </summary>
public sealed class RequiresAudioDeviceFactAttribute : FactAttribute
{
	public RequiresAudioDeviceFactAttribute()
	{
		if (!AudioDevices.HasRenderDevice)
		{
			Skip = "No active Windows audio output device is available.";
		}
	}
}

internal static class AudioDevices
{
	public static bool HasRenderDevice
	{
		get
		{
			if (!OperatingSystem.IsWindows())
			{
				return false;
			}

			try
			{
				using var enumerator = new MMDeviceEnumerator();
				return enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active).Count > 0;
			}
			catch (Exception exception) when (exception is COMException or InvalidOperationException or PlatformNotSupportedException)
			{
				return false;
			}
		}
	}
}
