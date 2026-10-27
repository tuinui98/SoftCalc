// InputRecall.cs - Arrow Up/Down through earlier inputs, keeping the current draft
//
// Like a command prompt: Up walks back from the newest input, Down walks
// forward, and going past the newest brings back what was being typed
// before the first Up. Submitting a line ends the walk.

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
		_position--;
		return inputs[_position];
	}

	// Returns the text to show, or null when not walking
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

	public void Reset()
	{
		_position = -1;
		_draft = "";
	}
}
