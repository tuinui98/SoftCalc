// EngineClient.cs - One engine process and the JSON-lines request/response channel
//
// Implements the transport in design/protocol.md: requests carry an id,
// a reader task matches each response line to its pending request. A
// dead process or a timeout fails the pending requests; restarting is
// EngineSession's job.

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace SoftCalc.Engine;

internal class EngineException : Exception
{
	public EngineException(string message) : base(message)
	{
	}
}

// The engine process died or stopped answering
internal sealed class EngineLostException : EngineException
{
	public EngineLostException(string message) : base(message)
	{
	}
}

// A request was made before the session started; not a lost engine, so no restart
internal sealed class EngineNotStartedException : EngineException
{
	public EngineNotStartedException() : base("Engine session has not started")
	{
	}
}

// The engine answered ok:false (a malformed request)
internal sealed class EngineRequestException : EngineException
{
	public EngineRequestException(string message) : base(message)
	{
	}
}

internal sealed class EngineClient : IDisposable
{
	private static readonly TimeSpan StartTimeout = TimeSpan.FromSeconds(20);

	private readonly Process _process;
	private readonly ConcurrentDictionary<int, TaskCompletionSource<JsonObject>> _pending = new();
	private readonly object _writeLock = new();
	private int _nextId;
	private bool _disposed;

	public string Version { get; }

	private EngineClient(Process process, string version)
	{
		_process = process;
		Version = version;
		_ = Task.Run(ReadLoop);
	}

	public static async Task<EngineClient> StartAsync(ProcessStartInfo info)
	{
		var process = Process.Start(info) ?? throw new EngineLostException("Engine process did not start");
		process.ErrorDataReceived += (_, _) => { };    // drain stderr so it never blocks
		process.BeginErrorReadLine();
		var readyTask = process.StandardOutput.ReadLineAsync();
		if (await Task.WhenAny(readyTask, Task.Delay(StartTimeout)) != readyTask || readyTask.Result == null)
		{
			Kill(process);
			throw new EngineLostException("Engine did not report ready");
		}
		var ready = JsonNode.Parse(readyTask.Result)?.AsObject();
		if (ready == null || (string?)ready["op"] != "ready" || (int?)ready["protocol"] != 1)
		{
			Kill(process);
			throw new EngineLostException("Engine speaks an unknown protocol");
		}
		return new EngineClient(process, (string?)ready["version"] ?? "?");
	}

	public bool IsAlive => !_disposed && !_process.HasExited;

	public async Task<JsonObject> RequestAsync(string op, JsonObject? arguments, TimeSpan timeout)
	{
		if (!IsAlive)
		{
			throw new EngineLostException("Engine is not running");
		}
		int id = Interlocked.Increment(ref _nextId);
		var request = new JsonObject { ["id"] = id, ["op"] = op };
		if (arguments != null)
		{
			foreach (var (key, value) in arguments)
			{
				request[key] = value?.DeepClone();
			}
		}
		var waiter = new TaskCompletionSource<JsonObject>(TaskCreationOptions.RunContinuationsAsynchronously);
		_pending[id] = waiter;
		try
		{
			lock (_writeLock)
			{
				_process.StandardInput.WriteLine(request.ToJsonString());
				_process.StandardInput.Flush();
			}
		}
		catch (IOException)
		{
			_pending.TryRemove(id, out _);
			throw new EngineLostException("Engine pipe closed");
		}
		if (await Task.WhenAny(waiter.Task, Task.Delay(timeout)) != waiter.Task)
		{
			_pending.TryRemove(id, out _);
			throw new EngineLostException($"Engine did not answer '{op}' within {timeout.TotalSeconds:0} s");
		}
		var response = await waiter.Task;
		if ((bool?)response["ok"] != true)
		{
			throw new EngineRequestException((string?)response["error"] ?? "request failed");
		}
		return response;
	}

	private async Task ReadLoop()
	{
		try
		{
			while (true)
			{
				string? line = await _process.StandardOutput.ReadLineAsync();
				if (line == null)
				{
					break;
				}
				JsonObject? response;
				try
				{
					response = JsonNode.Parse(line)?.AsObject();
				}
				catch (JsonException)
				{
					continue;
				}
				int? id = (int?)response?["id"];
				if (response != null && id != null && _pending.TryRemove(id.Value, out var waiter))
				{
					waiter.TrySetResult(response);
				}
			}
		}
		catch (Exception)
		{
			// Fall through: the process is gone either way
		}
		foreach (var id in _pending.Keys)
		{
			if (_pending.TryRemove(id, out var waiter))
			{
				waiter.TrySetException(new EngineLostException("Engine process ended"));
			}
		}
	}

	private static void Kill(Process process)
	{
		try
		{
			if (!process.HasExited)
			{
				process.Kill(entireProcessTree: true);
			}
		}
		catch (InvalidOperationException)
		{
		}
	}

	public void Dispose()
	{
		if (_disposed)
		{
			return;
		}
		_disposed = true;
		try
		{
			_process.StandardInput.Close();    // the engine exits when stdin closes
			if (!_process.WaitForExit(2000))
			{
				Kill(_process);
			}
		}
		catch (Exception)
		{
			Kill(_process);
		}
		_process.Dispose();
	}

	// For tests: simulate a crash
	internal void KillForTest()
	{
		Kill(_process);
	}
}
