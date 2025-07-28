using System;
using System.CodeDom.Compiler;
using System.Collections;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using ConstExpr.SourceGenerator.Extensions;
using Microsoft.CodeAnalysis;
using SourceGen.Utilities.Extensions;

namespace ConstExpr.SourceGenerator.Helpers;

// [DebuggerDisplay("{ToString()}")]
// public class IndentedStringBuilder(string indentString = "\t")
// {
// 	private IndentedTextWriter _builder = new(new StringWriter(), indentString);
// 	private bool _isNewLine = true;
//
// 	public static Compilation Compilation { get; set; }
//
// 	public IndentedStringBuilder Indent()
// 	{
// 		_builder.Indent++;
// 		return this;
// 	}
//
// 	public IndentedStringBuilder Outdent()
// 	{
// 		if (_builder.Indent > 0)
// 		{
// 			_builder.Indent--;
// 		}
// 		return this;
// 	}
//
// 	public IndentedStringBuilder AppendLine()
// 	{
// 		_builder.WriteLine();
// 		_isNewLine = true;
// 		return this;
// 	}
//
// 	public IndentedStringBuilder AppendLine(string? value = "")
// 	{
// 		if (string.IsNullOrEmpty(value))
// 		{
// 			_builder.WriteLine();
// 			_isNewLine = true;
// 			return this;
// 		}
//
// 		var lines = value.Split([ "\r\n", "\n" ], StringSplitOptions.None);
//
// 		for (var i = 0; i < lines.Length; i++)
// 		{
// 			_builder.Write(lines[i]);
//
// 			if (i < lines.Length - 1 || string.IsNullOrEmpty(value))
// 			{
// 				_builder.WriteLine();
// 				_isNewLine = true;
// 			}
// 			else
// 			{
// 				_isNewLine = false;
// 			}
// 		}
//
// 		if (!_isNewLine)
// 		{
// 			_builder.WriteLine();
// 			_isNewLine = true;
// 			return this;
// 		}
//
// 		return this;
// 	}
// 	
// 	public IndentedStringBuilder AppendLine([InterpolatedStringHandlerArgument("")] ref IndentedStringBuilderHandler handler)
// 	{
// 		// handler.CopyTo(_builder);
//
// 		_builder.WriteLine();
// 		_isNewLine = true;
// 		return this;
// 	}
//
// 	public IndentedStringBuilder Append(string value)
// 	{
// 		_builder.Write(value);
// 		_isNewLine = false;
// 		return this;
// 	}
// 	
// 	public IDisposable AppendBlock([InterpolatedStringHandlerArgument("")] ref IndentedStringBuilderHandler handler, WhitespacePadding padding = WhitespacePadding.None)
// 	{
// 		if (padding is WhitespacePadding.Before or WhitespacePadding.BeforeAndAfter)
// 		{
// 			AppendLine();
// 		}
// 		
// 		AppendLine(ref handler);
// 		AppendLine("{");
// 		Indent();
// 		
// 		return new ActionDisposable(() => Outdent().AppendLine("}"), padding);
// 	}
//
// 	public IDisposable AppendBlock(string text, WhitespacePadding padding = WhitespacePadding.None)
// 	{
// 		if (padding is WhitespacePadding.Before or WhitespacePadding.BeforeAndAfter)
// 		{
// 			AppendLine();
// 		}
// 		
// 		AppendLine(text);
// 		AppendLine("{");
// 		Indent();
// 		
// 		return new ActionDisposable(() => Outdent().AppendLine("}"), padding);
// 	}
//
// 	public IDisposable AppendBlock(string text, string end, WhitespacePadding padding = WhitespacePadding.None)
// 	{
// 		if (padding is WhitespacePadding.Before or WhitespacePadding.BeforeAndAfter)
// 		{
// 			AppendLine();
// 		}
// 		
// 		AppendLine(text);
// 		AppendLine("{");
// 		Indent();
// 		
// 		return new ActionDisposable(() => Outdent().AppendLine(end), padding);
// 	}
//
// 	public IDisposable AppendBlock([InterpolatedStringHandlerArgument("")] ref IndentedStringBuilderHandler handler, string end, WhitespacePadding padding = WhitespacePadding.None)
// 	{
// 		if (padding is WhitespacePadding.Before or WhitespacePadding.BeforeAndAfter)
// 		{
// 			AppendLine();
// 		}
// 		
// 		AppendLine(ref handler);
// 		AppendLine("{");
// 		Indent();
// 		
// 		return new ActionDisposable(() => Outdent().AppendLine(end), padding);
// 	}
//
// 	// private void AppendIndentation()
// 	// {
// 	// 	for (var i = 0; i < _indentLevel; i++)
// 	// 	{
// 	// 		_builder.Write(indentString);
// 	// 	}
// 	// }
//
// 	public IndentedStringBuilder Clear()
// 	{
// 		_builder = new IndentedTextWriter(new StringWriter());
// 		_isNewLine = true;
// 		return this;
// 	}
//
// 	public override string ToString()
// 	{
// 		return _builder.InnerWriter.ToString();
// 	}
//
// 	public readonly struct ActionDisposable(Func<IndentedStringBuilder> outdent, WhitespacePadding padding) : IDisposable
// 	{
// 		public void Dispose()
// 		{
// 			if (padding is WhitespacePadding.After or WhitespacePadding.BeforeAndAfter)
// 			{
// 				outdent().AppendLine();
// 			}
// 			else
// 			{
// 				outdent();
// 			}
// 		}
// 	}
//
// 	[InterpolatedStringHandler]
// 	public readonly ref struct IndentedStringBuilderHandler(int literalLength, int formattedCount, IndentedStringBuilder builder)
// 	{
// 		private readonly IndentedTextWriter _builder = builder._builder;
//
// 		public void AppendLiteral(string s)
// 		{
// 			var lines = s.Split([ "\r\n", "\n" ], StringSplitOptions.None);
//
// 			for (var i = 0; i < lines.Length; i++)
// 			{
// 				_builder.Write(lines[i]);
//
// 				if (i < lines.Length - 1 || string.IsNullOrEmpty(s))
// 				{
// 					_builder.WriteLine();
// 				}
// 			}
// 			
// 			//_builder.Write("\"");
// 			// builder.Append(s);
// 			// _builder.Write("\"");
// 		}
//
// 		public void AppendFormatted(IParameterSymbol parameter)
// 		{
// 			_builder.Write(parameter.Name);
// 		}
//
// 		public void AppendFormatted(ReadOnlySpan<char> data)
// 		{
// 			_builder.Write(data.ToString());
// 		}
//
// 		public void AppendFormatted(ITypeSymbol type)
// 		{
// 			_builder.Write(Compilation.GetMinimalString(type) ?? type.ToString());
// 		}
//
// 		public void AppendFormatted(ImmutableArray<IParameterSymbol> parameters)
// 		{
// 			for (var i = 0; i < parameters.Length; i++)
// 			{
// 				_builder.Write(parameters[i].Name);
//
// 				if (i < parameters.Length - 1)
// 				{
// 					_builder.Write(", ");
// 				}
// 			}
// 		}
//
// 		public void AppendFormatted(IEnumerable items)
// 		{
// 			var enumerator = items.GetEnumerator();
// 			
// 			try
// 			{
// 				if (!enumerator.MoveNext())
// 				{
// 					return;
// 				}
//
// 				_builder.Write(enumerator.Current);
//
// 				while (enumerator.MoveNext())
// 				{
// 					AppendLiteral(", ");
// 					AppendFormatted(enumerator.Current);
// 				}
// 			}
// 			finally
// 			{
// 				if (enumerator is IDisposable disposable)
// 				{
// 					disposable.Dispose();
// 				}
// 			}
// 		}
//
// 		public void AppendFormatted<T>(T value)
// 		{
// 			var literal = SyntaxHelpers.CreateLiteral(value);
// 			
// 			if (literal != null)
// 			{
// 				literal.WriteTo(_builder);
// 				return;
// 			}
// 			
// 			_builder.Write(value);
// 		}
//
// 		public void CopyTo(TextWriter target)
// 		{
// 			target.Write(_builder);
// 		}
// 	}
// }
//
// public enum WhitespacePadding
// {
// 	None,
// 	Before,
// 	After,
// 	BeforeAndAfter
// }
