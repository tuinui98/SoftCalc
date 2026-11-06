// EngineLocator.cs - Finds the engine to start: packaged exe or development venv
//
// Order: SOFTCALC_ENGINE environment variable (path to an exe), then the
// engine embedded in the published exe (unpacked under the data folder,
// M9), then softcalc-engine.exe next to SoftCalc.exe, then the
// repository's engine/.venv Python running `-m softcalc_engine`.

using System.Diagnostics;

namespace SoftCalc.Engine;

internal static class EngineLocator
{
	public const string ExeName = "softcalc-engine.exe";

	public static ProcessStartInfo Locate(string engineRoot)
	{
		string? overridePath = Environment.GetEnvironmentVariable("SOFTCALC_ENGINE");
		if (!string.IsNullOrEmpty(overridePath))
		{
			return ForExe(overridePath);
		}
		using (var embedded = EngineUnpacker.Embedded())
		{
			if (embedded != null)
			{
				return ForExe(EngineUnpacker.Unpack(embedded, engineRoot));
			}
		}
		string besideApp = Path.Combine(AppContext.BaseDirectory, ExeName);
		if (File.Exists(besideApp))
		{
			return ForExe(besideApp);
		}
		string? engineDir = FindDevEngine(AppContext.BaseDirectory);
		if (engineDir != null)
		{
			var info = Base(Path.Combine(engineDir, ".venv", "Scripts", "python.exe"));
			info.ArgumentList.Add("-m");
			info.ArgumentList.Add("softcalc_engine");
			info.WorkingDirectory = engineDir;
			return info;
		}
		throw new FileNotFoundException(
			$"Engine not found: put {ExeName} next to SoftCalc.exe, or set SOFTCALC_ENGINE.");
	}

	// Walks up from the app folder to the repository's engine/ folder with a venv
	internal static string? FindDevEngine(string start)
	{
		for (var dir = new DirectoryInfo(start); dir != null; dir = dir.Parent)
		{
			string candidate = Path.Combine(dir.FullName, "engine");
			if (File.Exists(Path.Combine(candidate, ".venv", "Scripts", "python.exe")))
			{
				return candidate;
			}
		}
		return null;
	}

	private static ProcessStartInfo ForExe(string path)
	{
		var info = Base(path);
		info.WorkingDirectory = Path.GetDirectoryName(path) ?? "";
		return info;
	}

	private static ProcessStartInfo Base(string fileName)
	{
		return new ProcessStartInfo(fileName)
		{
			UseShellExecute = false,
			CreateNoWindow = true,
			RedirectStandardInput = true,
			RedirectStandardOutput = true,
			RedirectStandardError = true,
			StandardInputEncoding = new System.Text.UTF8Encoding(false),
			StandardOutputEncoding = System.Text.Encoding.UTF8,
			StandardErrorEncoding = System.Text.Encoding.UTF8,
		};
	}
}
