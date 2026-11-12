// TpmRandom.cs - Random bytes from the TPM 2.0 chip (design/random_plan.md R5)
//
// Sends TPM2_GetRandom through the TPM Base Services (tbs.dll), which
// standard users may use. A machine without a TPM 2.0, or one whose
// policy blocks the command, gives a reason instead of bytes.

using System.Buffers.Binary;
using System.Runtime.InteropServices;

namespace SoftCalc.Logic;

internal static class TpmRandom
{
	private const uint TbsSuccess = 0;
	private const uint TpmVersion20 = 2;
	private const uint IncludeTpm20 = 1 << 2;    // TBS_CONTEXT_PARAMS2.includeTpm20
	private const uint PriorityNormal = 200;    // TBS_COMMAND_PRIORITY_NORMAL
	private const ushort NoSessions = 0x8001;    // TPM_ST_NO_SESSIONS
	private const uint GetRandomCode = 0x17B;    // TPM_CC_GetRandom

	[StructLayout(LayoutKind.Sequential)]
	private struct ContextParams
	{
		public uint Version;
		public uint Flags;
	}

	[DllImport("tbs.dll")]
	private static extern uint Tbsi_Context_Create(ref ContextParams parameters, out IntPtr context);

	[DllImport("tbs.dll")]
	private static extern uint Tbsip_Submit_Command(IntPtr context, uint locality, uint priority, byte[] command,
		uint commandSize, byte[] result, ref uint resultSize);

	[DllImport("tbs.dll")]
	private static extern uint Tbsip_Context_Close(IntPtr context);

	// count random bytes (up to 32 per command), or null and the reason
	public static (byte[]? Bytes, string? Error) Get(int count)
	{
		try
		{
			var parameters = new ContextParams { Version = TpmVersion20, Flags = IncludeTpm20 };
			uint status = Tbsi_Context_Create(ref parameters, out IntPtr context);
			if (status != TbsSuccess)
			{
				return (null, $"No TPM 2.0 available (TBS 0x{status:X8}).");
			}
			try
			{
				var bytes = new List<byte>();
				while (bytes.Count < count)
				{
					var (part, error) = GetRandom(context, (ushort)Math.Min(32, count - bytes.Count));
					if (part == null)
					{
						return (null, error);
					}
					bytes.AddRange(part);
				}
				return (bytes.ToArray(), null);
			}
			finally
			{
				Tbsip_Context_Close(context);
			}
		}
		catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
		{
			return (null, "TPM Base Services are not available on this Windows.");
		}
	}

	private static (byte[]? Bytes, string? Error) GetRandom(IntPtr context, ushort count)
	{
		var command = new byte[12];
		BinaryPrimitives.WriteUInt16BigEndian(command, NoSessions);
		BinaryPrimitives.WriteUInt32BigEndian(command.AsSpan(2), (uint)command.Length);
		BinaryPrimitives.WriteUInt32BigEndian(command.AsSpan(6), GetRandomCode);
		BinaryPrimitives.WriteUInt16BigEndian(command.AsSpan(10), count);
		var result = new byte[64];
		uint size = (uint)result.Length;
		uint status = Tbsip_Submit_Command(context, 0, PriorityNormal, command, (uint)command.Length, result, ref size);
		if (status != TbsSuccess)
		{
			return (null, $"The TPM refused GetRandom (TBS 0x{status:X8}).");
		}
		return Parse(result.AsSpan(0, (int)size));
	}

	// The response: tag, size, response code, then a TPM2B_DIGEST (size + bytes)
	internal static (byte[]? Bytes, string? Error) Parse(ReadOnlySpan<byte> response)
	{
		if (response.Length < 10)
		{
			return (null, "The TPM gave a short answer.");
		}
		uint code = BinaryPrimitives.ReadUInt32BigEndian(response[6..]);
		if (code != 0)
		{
			return (null, $"The TPM answered with error 0x{code:X}.");
		}
		if (response.Length < 12)
		{
			return (null, "The TPM gave a short answer.");
		}
		int length = BinaryPrimitives.ReadUInt16BigEndian(response[10..]);
		if (response.Length < 12 + length)
		{
			return (null, "The TPM gave a short answer.");
		}
		return (response.Slice(12, length).ToArray(), null);
	}
}
