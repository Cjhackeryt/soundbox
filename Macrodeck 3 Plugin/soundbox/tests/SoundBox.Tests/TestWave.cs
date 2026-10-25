namespace SoundBox.Tests;

/// <summary>
/// Writes a silent 16-bit PCM WAV of a requested length so playback tests do not
/// depend on a checked-in binary asset.
/// </summary>
internal static class TestWave
{
	public static string Create(string path, TimeSpan duration, int sampleRate = 8000)
	{
		var totalSamples = (int)(duration.TotalSeconds * sampleRate);
		var dataBytes = totalSamples * 2;

		using var stream = File.Create(path);
		using var writer = new BinaryWriter(stream);

		writer.Write("RIFF"u8);
		writer.Write(36 + dataBytes);
		writer.Write("WAVE"u8);
		writer.Write("fmt "u8);
		writer.Write(16);
		writer.Write((short)1);
		writer.Write((short)1);
		writer.Write(sampleRate);
		writer.Write(sampleRate * 2);
		writer.Write((short)2);
		writer.Write((short)16);
		writer.Write("data"u8);
		writer.Write(dataBytes);

		writer.Write(new byte[dataBytes]);
		writer.Flush();

		return path;
	}
}
