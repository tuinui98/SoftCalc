// PortableTests.cs - M9: data folder choice, embedded engine unpacking, notices

using System.IO.Compression;
using SoftCalc.Engine;
using SoftCalc.Persistence;
using SoftCalc.UI;

namespace SoftCalc.Tests;

public sealed class PortableTests : IDisposable
{
	private readonly string _dir = Path.Combine(Path.GetTempPath(), "softcalc-m9-" + Guid.NewGuid().ToString("N"));

	public void Dispose()
	{
		if (Directory.Exists(_dir))
		{
			Directory.Delete(_dir, recursive: true);
		}
	}

	[Fact]
	public void DataFolderOrder()
	{
		string exe = @"D:\Apps\SoftCalc", appData = @"C:\Users\u\AppData\Roaming";
		Assert.Equal(@"X:\test", DataFolder.Choose(@"X:\test", true, exe, appData, _ => true));
		Assert.Equal(@"D:\Apps\SoftCalc\SoftCalc-data", DataFolder.Choose(null, true, exe, appData, _ => true));
		Assert.Equal(@"C:\Users\u\AppData\Roaming\SoftCalc", DataFolder.Choose("", true, exe, appData, _ => false));    // read-only folder
		Assert.Equal(@"C:\Users\u\AppData\Roaming\SoftCalc", DataFolder.Choose(null, false, exe, appData, _ => true));    // development build
	}

	private static MemoryStream Zip(string content)
	{
		var stream = new MemoryStream();
		using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
		{
			using (var writer = new StreamWriter(zip.CreateEntry(EngineLocator.ExeName).Open()))
			{
				writer.Write(content);
			}
			zip.CreateEntry("_internal/python.dll").Open().Dispose();
		}
		stream.Position = 0;
		return stream;
	}

	[Fact]
	public void UnpacksOnceAndReuses()
	{
		string exe = EngineUnpacker.Unpack(Zip("v1"), _dir);
		Assert.Equal("v1", File.ReadAllText(exe));
		Assert.True(File.Exists(Path.Combine(Path.GetDirectoryName(exe)!, "_internal", "python.dll")));
		File.WriteAllText(exe, "kept");
		Assert.Equal(exe, EngineUnpacker.Unpack(Zip("v1"), _dir));
		Assert.Equal("kept", File.ReadAllText(exe));    // same hash: not unpacked again
		Assert.Single(Directory.GetDirectories(_dir));
	}

	[Fact]
	public void NewVersionReplacesTheOldFolder()
	{
		string first = EngineUnpacker.Unpack(Zip("v1"), _dir);
		string second = EngineUnpacker.Unpack(Zip("v2"), _dir);
		Assert.NotEqual(first, second);
		Assert.False(File.Exists(first));
		Assert.Single(Directory.GetDirectories(_dir));
	}

	[Fact]
	public void HalfWrittenFolderIsRedone()
	{
		string exe = EngineUnpacker.Unpack(Zip("v1"), _dir);
		string folder = Path.GetDirectoryName(exe)!;
		File.Delete(Path.Combine(folder, ".complete"));    // as if a run was stopped halfway
		File.WriteAllText(exe, "broken");
		Assert.Equal("v1", File.ReadAllText(EngineUnpacker.Unpack(Zip("v1"), _dir)));
	}

	[Fact]
	public void NoticesAreEmbedded()
	{
		string text = AboutDialog.Notices();
		Assert.Contains("mpmath", text);
		Assert.Contains("PYTHON SOFTWARE FOUNDATION LICENSE", text);
		Assert.Contains("IconMCP", text);
	}
}
