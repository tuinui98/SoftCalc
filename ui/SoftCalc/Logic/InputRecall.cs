// InputRecall.cs - Arrow Up/Down through earlier inputs, keeping the current draft
//
// Like a command prompt: Up walks back from the newest input, Down walks
// forward, and going past the newest brings back what was being typed
// before the walk began. Owner 2026-11-16: the walk shows as the
// highlighted History line; a click on a line starts the walk there (Up
// and Down go on from it), and Esc or a second click on the same line
// ends it with the draft back. Submitting or typing ends the walk.

namespace SoftCalc.Logic;

internal sealed class InputRecall
{
	private readonly Func<IReadOnlyList<string>> _inputs;
	private int _position = -1;    // -1: not walking; else index into inputs, newest = Count - 1
	private string _draft = "";

	public InputRecall(Func<IReadOnlyList<string>> inputs)
	{
		_inputs = inputs;
	}

	// Index into the inputs of the line shown, or -1 when not walking
	public int Position => _position;

	// Returns the text to show, or null when there is nothing older
	public string? Up(string currentText)
	{
		var inputs = _inputs();
		if (inputs.Count == 0)
		{
			return null;
		}
		if (_position == -1)
		{
			_draft = currentText;
			_position = inputs.Count;
		}
		if (_position == 0)
		{
			return null;
		}
		_position = Math.Min(_position, inputs.Count) - 1;
		return inputs[_position];
	}

	// Returns the text to show, or null when not walking; past the newest, the draft
	public string? Down()
	{
		var inputs = _inputs();
		if (_position == -1)
		{
			return null;
		}
		_position++;
		if (_position >= inputs.Count)
		{
			_position = -1;
			return _draft;
		}
		return inputs[_position];
	}

	// A click on a line: the walk goes there (the draft is kept from before the walk)
	public string? Jump(int position, string currentText)
	{
		var inputs = _inputs();
		if (position < 0 || position >= inputs.Count)
		{
			return null;
		}
		if (_position == -1)
		{
			_draft = currentText;
		}
		_position = position;
		return inputs[position];
	}

	// Esc or a second click: the walk ends and the draft comes back; null when not walking
	public string? Cancel()
	{
		if (_position == -1)
		{
			return null;
		}
		_position = -1;
		return _draft;
	}

	// The user typed: what is in Input is now the draft
	public void Edited()
	{
		_position = -1;
	}

	public void Reset()
	{
		_position = -1;
		_draft = "";
	}
}
