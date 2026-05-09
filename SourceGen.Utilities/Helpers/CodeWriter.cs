using System.Collections.Generic;
using System.Text;

namespace SourceGen.Utilities.Helpers;

public sealed class CodeWriter
{
	private readonly StringBuilder _builder = new();
	private readonly Stack<string> _indentation = new();

	public CodeWriter WriteLine(string line)
	{
		foreach (var indent in _indentation)
		{
			_builder.Append(indent);
		}

		_builder.AppendLine(line);

		return this;
	}

	public CodeWriter AddIndent(string indent)
	{
		_indentation.Push(indent);
		return this;
	}

	public CodeWriter RemoveIndent()
	{
		if (_indentation.Count > 0)
		{
			_indentation.Pop();
		}

		return this;
	}

	public override string ToString()
	{
		return _builder.ToString();
	}
}