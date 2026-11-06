// EngineUnpacker.cs - Unpacks the engine embedded in the published exe (M9)
//
// The single-file SoftCalc.exe carries the PyInstaller one-folder engine
// as engine.zip. It is unpacked once to <data>\engine\<hash>\, where the
// hash is the start of the zip's SHA-256: into a temporary folder first,
// a .complete marker written last, then renamed, so a window that starts
// at the same time, or a run that was killed halfway, never uses a
// half-written engine. Folders of older hashes are removed when unused.

using System.IO.Compression;
using System.Reflection;
using System.Security.Cryptography;

namespace SoftCalc.Engine;

internal static class EngineUnpacker
{
	public const string ResourceName = "engine.zip";
	private const string Marker = ".complete";

	// The embedded zip, or null in development builds
	public static Stream? Embedded()
	{
		return Assembly.GetExecutingAssembly().GetManifestResourceStream(ResourceName);
	}

	// Returns the engine exe inside root\<hash>\, unpacking when needed
	public static string Unpack(Stream zip, string root)
	{
		using var buffer = new MemoryStream();
		zip.CopyTo(buffer);
		string hash = Convert.ToHexString(SHA256.HashData(buffer.ToArray()))[..16].ToLowerInvariant();
		string target = Path.Combine(root, hash);
		string exe = Path.Combine(target, EngineLocator.ExeName);
		if (!File.Exists(Path.Combine(target, Marker)) || !File.Exists(exe))
		{
			Directory.CreateDirectory(root);
			string temp = Path.Combine(root, $"{hash}.tmp-{Environment.ProcessId}");
			if (Directory.Exists(temp))
			{
				Directory.Delete(temp, recursive: true);
			}
			buffer.Position = 0;
			ZipFile.ExtractToDirectory(buffer, temp);
			File.WriteAllText(Path.Combine(temp, Marker), hash);
			if (Directory.Exists(target))
			{
				TryDelete(target);    // left by a run that was stopped halfway
			}
			try
			{
				Directory.Move(temp, target);
			}
			catch (IOException) when (File.Exists(Path.Combine(target, Marker)))
			{
				TryDelete(temp);    // another window finished first
			}
		}
		RemoveOthers(root, hash);
		return exe;
	}

	// Older versions; a folder whose engine still runs (another window) stays
	private static void RemoveOthers(string root, string keep)
	{
		foreach (string dir in Directory.GetDirectories(root))
		{
			string name = Path.GetFileName(dir);
			bool unpacking = name.Contains(".tmp-", StringComparison.Ordinal)
				&& Directory.GetLastWriteTimeUtc(dir) > DateTime.UtcNow.AddMinutes(-10);    // another window, now
			if (name != keep && !unpacking)
			{
				TryDelete(dir);
			}
		}
	}

	private static void TryDelete(string dir)
	{
		try
		{
			Directory.Delete(dir, recursive: true);
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
		{
			// in use; removed on a later start
		}
	}
}
