// SeedEngine.cs - The Random window's own engine for Seed (design/random_plan.md R4)
//
// Seed draws go through an engine process of the window's own, so they
// are the values `rands(seed)` and the same lines typed in Input give,
// and the main window's seed is left alone. It starts on the first
// seeded Generate and stops when the window closes. The main window's
// precision is applied before each batch, since decimal draws have as
// many digits as the precision.

using System.Diagnostics;
using System.Text.Json.Nodes;

namespace SoftCalc.Engine;

internal sealed class SeedEngine : IDisposable
{
	private readonly ProcessStartInfo _startInfo;
	private readonly Func<int> _precision;
	private EngineSession? _session;

	public SeedEngine(ProcessStartInfo startInfo, Func<int> precision)
	{
		_startInfo = startInfo;
		_precision = precision;
	}

	public bool IsStarted => _session != null;

	public async Task<(List<string>? Texts, string? Error)> DrawsAsync(string seed, string line, int count, bool unique)
	{
		try
		{
			if (_session == null)
			{
				var session = new EngineSession(_startInfo);
				await session.StartAsync();
				_session = session;
			}
			await _session.ChangeSettingsAsync(new JsonObject { ["precision"] = _precision() });
		}
		catch (EngineException ex)
		{
			return (null, "The engine for Seed did not start: " + ex.Message);
		}
		return await _session.DrawsAsync(seed, line, count, unique);
	}

	public void Dispose()
	{
		_session?.Dispose();
		_session = null;
	}
}
