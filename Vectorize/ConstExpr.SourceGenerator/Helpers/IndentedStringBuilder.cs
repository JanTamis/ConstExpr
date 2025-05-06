using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using ConstExpr.SourceGenerator.Extensions;
using Microsoft.CodeAnalysis;

namespace ConstExpr.SourceGenerator.Helpers;

[DebuggerDisplay("{ToString()}")]
public class IndentedStringBuilder(string indentString = "\t")
{
	private readonly StringBuilder _builder = new();
	private int _indentLevel;
	private bool _isNewLine = true;

	public static Compilation Compilation { get; set; }

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

	public IndentedStringBuilder AppendLine()
	{
		if (_isNewLine)
		{
			AppendIndentation();
		}

		_builder.AppendLine();
		_isNewLine = true;
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
	
	public IndentedStringBuilder AppendLine(ref IndentedStringBuilderHandler handler)
	{
		if (handler.Length == 0)
		{
			if (_isNewLine)
			{
				AppendIndentation();
			}

			_builder.AppendLine();
			_isNewLine = true;
			return this;
		}

		if (_isNewLine)
		{
			AppendIndentation();
		}

		handler.CopyTo(_builder);

		_builder.AppendLine();
		_isNewLine = true;
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
	
	public IDisposable AppendBlock(ref IndentedStringBuilderHandler handler)
	{
		AppendLine(ref handler);
		AppendLine("{");
		Indent();
		return new ActionDisposable(() => Outdent().AppendLine("}"));
	}

	public IDisposable AppendBlock(string text)
	{
		AppendLine(text);
		AppendLine("{");
		Indent();
		return new ActionDisposable(() => Outdent().AppendLine("}"));
	}

	public IDisposable AppendBlock(string text, string end)
	{
		AppendLine(text);
		AppendLine("{");
		Indent();
		return new ActionDisposable(() => Outdent().AppendLine(end));
	}

	public IDisposable AppendBlock(ref IndentedStringBuilderHandler handler, string end)
	{
		AppendLine(ref handler);
		AppendLine("{");
		Indent();
		return new ActionDisposable(() => Outdent().AppendLine(end));
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

	[InterpolatedStringHandler]
	public readonly struct IndentedStringBuilderHandler(int literalLength, int formattedCount)
	{
		private readonly StringBuilder _builder = new(literalLength + formattedCount * 11);

		public int Length => _builder.Length;

		public void AppendLiteral(string s)
		{
			_builder.Append(s);
		}

		public void AppendFormatted(IParameterSymbol parameter)
		{
			_builder.Append(parameter.Name);
		}

		public void AppendFormatted(ReadOnlySpan<char> data)
		{
			_builder.Append(data.ToString());
		}

		public void AppendFormatted(ITypeSymbol type)
		{
			_builder.Append(Compilation.GetMinimalString(type));
		}

		public void AppendFormatted(LiteralString literal)
		{
			AppendLiteral(literal.Value);
		}

		public void AppendFormatted(ImmutableArray<IParameterSymbol> parameters)
		{
			for (var i = 0; i < parameters.Length; i++)
			{
				_builder.Append(parameters[i].Name);

				if (i < parameters.Length - 1)
				{
					_builder.Append(", ");
				}
			}
		}

		public void AppendFormatted(IEnumerable items)
		{
			var enumerator = items.GetEnumerator();

			if (!enumerator.MoveNext())
			{
				return;
			}

			_builder.Append(enumerator.Current);

			while (enumerator.MoveNext())
			{
				AppendLiteral(", ");
				AppendFormatted(enumerator.Current);
			}
		}

		public void AppendFormatted<T>(T value)
		{
			// if (value is string str)
			// {
			// 	_builder.Append(str);
			// 	return;
			// }

			_builder.Append(SyntaxHelpers.CreateLiteral(value)?.ToString() ?? value.ToString());
		}

		public void CopyTo(StringBuilder target)
		{
			target.Append(_builder);
		}
	}
}

