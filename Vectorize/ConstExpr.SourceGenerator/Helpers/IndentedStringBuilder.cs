using System;
using System.Text;

namespace ConstExpr.SourceGenerator.Helpers;

public class IndentedStringBuilder(string indentString = "\t")
{
	private readonly StringBuilder _builder = new();
	private int _indentLevel;
	private bool _isNewLine = true;

	public IndentedStringBuilder Indent()
	{
		_indentLevel++;
		return this;
	}

	public IndentedStringBuilder Outdent()
	{
		if (_indentLevel > 0)
		{
			_indentLevel--;
		}
		return this;
	}

	public IndentedStringBuilder AppendLine(string? value = "")
	{
		if (string.IsNullOrEmpty(value))
		{
			if (_isNewLine)
			{
				AppendIndentation();
			}

			_builder.AppendLine();
			_isNewLine = true;
			return this;
		}

		var lines = value.Split([ "\r\n", "\n" ], StringSplitOptions.None);

		for (var i = 0; i < lines.Length; i++)
		{
			if (_isNewLine)
			{
				AppendIndentation();
			}

			_builder.Append(lines[i]);

			if (i < lines.Length - 1 || string.IsNullOrEmpty(value))
			{
				_builder.AppendLine();
				_isNewLine = true;
			}
			else
			{
				_isNewLine = false;
			}
		}

		if (!_isNewLine)
		{
			_builder.AppendLine();
			_isNewLine = true;
			return this;
		}

		return this;
	}

	public IndentedStringBuilder Append(string value)
	{
		if (_isNewLine && !string.IsNullOrEmpty(value))
		{
			AppendIndentation();
		}

		_builder.Append(value);
		_isNewLine = false;
		return this;
	}
	
	public IDisposable AppendBlock(string text)
	{
		AppendLine(text);
		AppendLine("{");
		Indent();
		return new ActionDisposable(() => Outdent().AppendLine("}"));
	}

	private void AppendIndentation()
	{
		for (var i = 0; i < _indentLevel; i++)
		{
			_builder.Append(indentString);
		}
	}

	public IndentedStringBuilder Clear()
	{
		_builder.Clear();
		_isNewLine = true;
		return this;
	}

	public int Length => _builder.Length;

	public override string ToString()
	{
		return _builder.ToString();
	}

	public class ActionDisposable(Func<IndentedStringBuilder> outdent) : IDisposable
	{
		public void Dispose()
		{
			outdent();
		}
	}
}

