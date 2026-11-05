// PcfFile.cs - Read and write Functions & Variables files (SPEC 8)
//
// Saved like the original: UTF-16 LE without BOM, CRLF lines (the engine
// supplies the text). Reading accepts UTF-16 LE/BE and UTF-8, with or
// without a BOM, so files from the original and hand-edited files load.

using System.Text;

namespace SoftCalc.Persistence;

internal static class PcfFile
{
	public static byte[] Encode(string text)
	{
		return new UnicodeEncoding(bigEndian: false, byteOrderMark: false).GetBytes(text);
	}

	public static string Decode(byte[] bytes)
	{
		if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
		{
			return Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3);
		}
		if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
		{
			return Encoding.Unicode.GetString(bytes, 2, bytes.Length - 2);
		}
		if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF)
		{
			return Encoding.BigEndianUnicode.GetString(bytes, 2, bytes.Length - 2);
		}
		return LooksUtf16Le(bytes) ? Encoding.Unicode.GetString(bytes) : Encoding.UTF8.GetString(bytes);
	}

	// ASCII text in UTF-16 LE has a zero in most odd bytes; UTF-8 text has none
	private static bool LooksUtf16Le(byte[] bytes)
	{
		if (bytes.Length < 2 || bytes.Length % 2 != 0)
		{
			return false;
		}
		int pairs = bytes.Length / 2;
		int zeros = 0;
		for (int i = 1; i < bytes.Length; i += 2)
		{
			if (bytes[i] == 0)
			{
				zeros++;
			}
		}
		return zeros * 2 > pairs;
	}

	public static string Read(string path)
	{
		return Decode(File.ReadAllBytes(path));
	}

	public static void Write(string path, string text)
	{
		DataFolder.WriteAtomic(path, Encode(text));
	}
}
