// DataFolder.cs - Where SoftCalc keeps its files, and safe whole-file writes
//
// SOFTCALC_DATA_DIR wins (tests and scripted runs use their own folder);
// the published single-file exe keeps its files in SoftCalc-data\ beside
// itself when that can be written (portable, M9), else %APPDATA%\SoftCalc,
// which is also where development builds keep them. Writes take a short
// exclusive lock shared by all SoftCalc windows, write a temp file, then
// replace.

using System.Text;

namespace SoftCalc.Persistence;

internal sealed class DataFolder
{
	public const string PortableName = "SoftCalc-data";
	private static readonly TimeSpan LockTimeout = TimeSpan.FromSeconds(3);

	public string Root { get; }

	public DataFolder(string root)
	{
		Root = root;
		Directory.CreateDirectory(root);
	}

	public static DataFolder Default()
	{
		// The published single-file exe has no SoftCalc.dll beside it; a build output has
		bool published = !File.Exists(Path.Combine(AppContext.BaseDirectory, "SoftCalc.dll"));
		return new DataFolder(Choose(
			Environment.GetEnvironmentVariable("SOFTCALC_DATA_DIR"), published, AppContext.BaseDirectory,
			Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), CanWrite));
	}

	// The data folder rule above, with the file system check passed in for tests
	internal static string Choose(string? overrideDir, bool published, string exeDir, string appData, Func<string, bool> canWrite)
	{
		if (!string.IsNullOrEmpty(overrideDir))
		{
			return overrideDir;
		}
		string portable = Path.Combine(exeDir, PortableName);
		if (published && canWrite(portable))
		{
			return portable;
		}
		return Path.Combine(appData, "SoftCalc");
	}

	// Creates the folder if needed and writes a probe file (Program Files is not writable)
	private static bool CanWrite(string folder)
	{
		try
		{
			Directory.CreateDirectory(folder);
			string probe = Path.Combine(folder, ".write-test");
			File.WriteAllText(probe, "");
			File.Delete(probe);
			return true;
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
		{
			return false;
		}
	}

	public string EngineRoot => Path.Combine(Root, "engine");
	public string SettingsPath => Path.Combine(Root, "settings.json");
	public string DefinitionsPath => Path.Combine(Root, "definitions.pcf");
	public string HistoryPath => Path.Combine(Root, "history.jsonl");
	public string RatesPath => Path.Combine(Root, "rates.xml");
	private string LockPath => Path.Combine(Root, "softcalc.lock");

	// Runs action while holding the folder lock; other windows wait briefly
	public T WithLock<T>(Func<T> action)
	{
		var deadline = DateTime.UtcNow + LockTimeout;
		while (true)
		{
			try
			{
				using var handle = new FileStream(LockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
				return action();
			}
			catch (IOException) when (DateTime.UtcNow < deadline)
			{
				Thread.Sleep(50);
			}
		}
	}

	public void WithLock(Action action)
	{
		WithLock(() =>
		{
			action();
			return 0;
		});
	}

	// Atomic replace: readers see the old file or the new one, never half of one
	public static void WriteAtomic(string path, byte[] content)
	{
		string temp = path + ".tmp";
		File.WriteAllBytes(temp, content);
		File.Move(temp, path, overwrite: true);
	}

	public static void WriteAtomic(string path, string text)
	{
		WriteAtomic(path, new UTF8Encoding(false).GetBytes(text));
	}
}
