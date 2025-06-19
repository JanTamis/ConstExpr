using System;
using System.CodeDom.Compiler;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using ConstExpr.SourceGenerator.Extensions;
using Microsoft.CodeAnalysis;

namespace ConstExpr.SourceGenerator.Helpers;

[DebuggerDisplay("{ToString()}")]
public class IndentedStringBuilder(string indentString = "\t")
{
	private StringWriter _builder = new();
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

		_builder.WriteLine();
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

			_builder.WriteLine();
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

			_builder.Write(lines[i]);

			if (i < lines.Length - 1 || string.IsNullOrEmpty(value))
			{
				_builder.WriteLine();
				_isNewLine = true;
			}
			else
			{
				_isNewLine = false;
			}
		}

		if (!_isNewLine)
		{
			_builder.WriteLine();
			_isNewLine = true;
			return this;
		}

		return this;
	}
	
	public IndentedStringBuilder AppendLine(ref IndentedStringBuilderHandler handler)
	{
		if (_isNewLine)
		{
			AppendIndentation();
		}

		handler.CopyTo(_builder);

		_builder.WriteLine();
		_isNewLine = true;
		return this;
	}

	public IndentedStringBuilder Append(string value)
	{
		if (_isNewLine && !string.IsNullOrEmpty(value))
		{
			AppendIndentation();
		}

		_builder.Write(value);
		_isNewLine = false;
		return this;
	}
	
	public IDisposable AppendBlock(ref IndentedStringBuilderHandler handler, WhitespacePadding padding = WhitespacePadding.None)
	{
		if (padding is WhitespacePadding.Before or WhitespacePadding.BeforeAndAfter)
		{
			AppendLine();
		}
		
		AppendLine(ref handler);
		AppendLine("{");
		Indent();
		
		return new ActionDisposable(() => Outdent().AppendLine("}"), padding);
	}

	public IDisposable AppendBlock(string text, WhitespacePadding padding = WhitespacePadding.None)
	{
		if (padding is WhitespacePadding.Before or WhitespacePadding.BeforeAndAfter)
		{
			AppendLine();
		}
		
		AppendLine(text);
		AppendLine("{");
		Indent();
		
		return new ActionDisposable(() => Outdent().AppendLine("}"), padding);
	}

	public IDisposable AppendBlock(string text, string end, WhitespacePadding padding = WhitespacePadding.None)
	{
		if (padding is WhitespacePadding.Before or WhitespacePadding.BeforeAndAfter)
		{
			AppendLine();
		}
		
		AppendLine(text);
		AppendLine("{");
		Indent();
		
		return new ActionDisposable(() => Outdent().AppendLine(end), padding);
	}

	public IDisposable AppendBlock(ref IndentedStringBuilderHandler handler, string end, WhitespacePadding padding = WhitespacePadding.None)
	{
		if (padding is WhitespacePadding.Before or WhitespacePadding.BeforeAndAfter)
		{
			AppendLine();
		}
		
		AppendLine(ref handler);
		AppendLine("{");
		Indent();
		
		return new ActionDisposable(() => Outdent().AppendLine(end), padding);
	}

	private void AppendIndentation()
	{
		for (var i = 0; i < _indentLevel; i++)
		{
			_builder.Write(indentString);
		}
	}

	public IndentedStringBuilder Clear()
	{
		_builder = new StringWriter();
		_isNewLine = true;
		return this;
	}

	public override string ToString()
	{
		return _builder.ToString();
	}

	public readonly struct ActionDisposable(Func<IndentedStringBuilder> outdent, WhitespacePadding padding) : IDisposable
	{
		public void Dispose()
		{
			if (padding is WhitespacePadding.After or WhitespacePadding.BeforeAndAfter)
			{
				outdent().AppendLine();
			}
			else
			{
				outdent();
			}
		}
	}

	[InterpolatedStringHandler]
	public readonly ref struct IndentedStringBuilderHandler(int literalLength, int formattedCount)
	{
		private readonly StringWriter _builder = new(new StringBuilder(literalLength + formattedCount * 11));

		public void AppendLiteral(string s)
		{
			_builder.Write(s);
		}

		public void AppendFormatted(IParameterSymbol parameter)
		{
			_builder.Write(parameter.Name);
		}

		public void AppendFormatted(ReadOnlySpan<char> data)
		{
			_builder.Write(data.ToString());
		}

		public void AppendFormatted(ITypeSymbol type)
		{
			_builder.Write(Compilation.GetMinimalString(type));
		}

		public void AppendFormatted(LiteralString literal)
		{
			AppendLiteral(literal.Value);
		}

		public void AppendFormatted(ImmutableArray<IParameterSymbol> parameters)
		{
			for (var i = 0; i < parameters.Length; i++)
			{
				_builder.Write(parameters[i].Name);

				if (i < parameters.Length - 1)
				{
					_builder.Write(", ");
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

			_builder.Write(enumerator.Current);

			while (enumerator.MoveNext())
			{
				AppendLiteral(", ");
				AppendFormatted(enumerator.Current);
			}
		}

		public void AppendFormatted<T>(T value)
		{
			var literal = SyntaxHelpers.CreateLiteral(value);
			
			if (literal != null)
			{
				literal.WriteTo(_builder);
				return;
			}
			
			_builder.Write(value);
		}

		public void CopyTo(StringWriter target)
		{
			target.Write(_builder);
		}
	}
}

public enum WhitespacePadding
{
	None,
	Before,
	After,
	BeforeAndAfter
}

