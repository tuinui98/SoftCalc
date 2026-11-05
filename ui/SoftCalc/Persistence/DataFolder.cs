// DataFolder.cs - Where SoftCalc keeps its files, and safe whole-file writes
//
// %APPDATA%\SoftCalc by default; SOFTCALC_DATA_DIR overrides it (tests and
// scripted runs use their own folder). Writes take a short exclusive
// lock shared by all SoftCalc windows, write a temp file, then replace.

using System.Text;

namespace SoftCalc.Persistence;

internal sealed class DataFolder
{
	private static readonly TimeSpan LockTimeout = TimeSpan.FromSeconds(3);

	public string Root { get; }

	public DataFolder(string root)
	{
		Root = root;
		Directory.CreateDirectory(root);
	}

	public static DataFolder Default()
	{
		string? overrideDir = Environment.GetEnvironmentVariable("SOFTCALC_DATA_DIR");
		string root = !string.IsNullOrEmpty(overrideDir)
			? overrideDir
			: Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SoftCalc");
		return new DataFolder(root);
	}

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
