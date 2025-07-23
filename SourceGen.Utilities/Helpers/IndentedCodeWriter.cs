using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using System.Threading;

namespace SourceGen.Utilities.Helpers;

/// <summary>
/// A helper type to build sequences of values with pooled buffers.
/// </summary>
public sealed class IndentedCodeWriter : IDisposable
{
	/// <summary>
	/// The default indentation (tab).
	/// </summary>
	private const string DefaultIndentation = "\t";

	/// <summary>
	/// The default new line (<c>'\n'</c>).
	/// </summary>
	private const char DefaultNewLine = '\n';

	/// <summary>
	/// The <see cref="ImmutableArrayBuilder{T}"/> instance that text will be written to.
	/// </summary>
	private ImmutableArrayBuilder<char> _builder;

	/// <summary>
	/// The current indentation level.
	/// </summary>
	private int _currentIndentationLevel;

	/// <summary>
	/// The current indentation, as text.
	/// </summary>
	private string _currentIndentation = String.Empty;

	/// <summary>
	/// The cached array of available indentations, as text.
	/// </summary>
	private string[] _availableIndentations;

	/// <summary>
	/// Creates a new <see cref="IndentedCodeWriter"/> object.
	/// </summary>
	public IndentedCodeWriter()
	{
		_builder = new ImmutableArrayBuilder<char>();
		_currentIndentationLevel = 0;
		_currentIndentation = String.Empty;
		_availableIndentations = new string[4];
		_availableIndentations[0] = String.Empty;

		for (int i = 1, n = _availableIndentations.Length; i < n; i++)
		{
			_availableIndentations[i] = _availableIndentations[i - 1] + DefaultIndentation;
		}
	}

	/// <summary>
	/// Advances the current writer and gets a <see cref="Span{T}"/> to the requested memory area.
	/// </summary>
	/// <param name="requestedSize">The requested size to advance by.</param>
	/// <returns>A <see cref="Span{T}"/> to the requested memory area.</returns>
	/// <remarks>
	/// No other data should be written to the writer while the returned <see cref="Span{T}"/>
	/// is in use, as it could invalidate the memory area wrapped by it, if resizing occurs.
	/// </remarks>
	public Span<char> Advance(int requestedSize)
	{
		// Add the leading whitespace if needed (same as WriteRawText below)
		if (_builder.Count == 0 || _builder.WrittenSpan[^1] == DefaultNewLine)
		{
			_builder.AddRange(_currentIndentation.AsSpan());
		}

		return _builder.Advance(requestedSize);
	}

	/// <summary>
	/// Increases the current indentation level.
	/// </summary>
	public void IncreaseIndent()
	{
		_currentIndentationLevel++;

		if (_currentIndentationLevel == _availableIndentations.Length)
		{
			Array.Resize(ref _availableIndentations, _availableIndentations.Length * 2);
		}

		// Set both the current indentation and the current position in the indentations
		// array to the expected indentation for the incremented level (ie. one level more).
		_currentIndentation = _availableIndentations[_currentIndentationLevel]
			??= _availableIndentations[_currentIndentationLevel - 1] + DefaultIndentation;
	}

	/// <summary>
	/// Decreases the current indentation level.
	/// </summary>
	public void DecreaseIndent()
	{
		_currentIndentationLevel--;
		_currentIndentation = _availableIndentations[_currentIndentationLevel];
	}

	/// <summary>
	/// Writes a block to the underlying buffer.
	/// </summary>
	/// <param name="start">The opening string to use for the block.</param>
	/// <param name="end">The closing string to use for the block.</param>
	/// <returns>A <see cref="Block"/> value to close the open block with.</returns>
	public Block WriteBlock(string start = "{", string end = "}")
	{
		WriteLine(start);
		IncreaseIndent();

		return new Block(this, end);
	}

	/// <summary>
	/// Writes content to the underlying buffer.
	/// </summary>
	/// <param name="content">The content to write.</param>
	/// <param name="isMultiline">Whether the input content is multiline.</param>
	public void Write(string content, bool isMultiline = false)
	{
		Write(content.AsSpan(), isMultiline);
	}

	/// <summary>
	/// Writes content to the underlying buffer.
	/// </summary>
	/// <param name="content">The content to write.</param>
	/// <param name="isMultiline">Whether the input content is multiline.</param>
	public void Write(ReadOnlySpan<char> content, bool isMultiline = false)
	{
		if (isMultiline)
		{
			while (content.Length > 0)
			{
				var newLineIndex = content.IndexOf(DefaultNewLine);

				if (newLineIndex < 0)
				{
					// There are no new lines left, so the content can be written as a single line
					WriteRawText(content);

					break;
				}

				var line = content[..newLineIndex];

				// Write the current line (if it's empty, we can skip writing the text entirely).
				// This ensures that raw multiline string literals with blank lines don't have
				// extra whitespace at the start of those lines, which would otherwise happen.
				WriteIf(!line.IsEmpty, line);
				WriteLine();

				// Move past the new line character (the result could be an empty span)
				content = content[(newLineIndex + 1)..];
			}
		}
		else
		{
			WriteRawText(content);
		}
	}

	/// <summary>
	/// Writes content to the underlying buffer.
	/// </summary>
	/// <param name="handler">The interpolated string handler with content to write.</param>
	public void Write([InterpolatedStringHandlerArgument("")] ref WriteInterpolatedStringHandler handler)
	{
		_ = this;
	}

	/// <summary>
	/// Writes content to the underlying buffer depending on an input condition.
	/// </summary>
	/// <param name="condition">The condition to use to decide whether or not to write content.</param>
	/// <param name="content">The content to write.</param>
	/// <param name="isMultiline">Whether the input content is multiline.</param>
	public void WriteIf(bool condition, string content, bool isMultiline = false)
	{
		if (condition)
		{
			Write(content.AsSpan(), isMultiline);
		}
	}

	/// <summary>
	/// Writes content to the underlying buffer depending on an input condition.
	/// </summary>
	/// <param name="condition">The condition to use to decide whether or not to write content.</param>
	/// <param name="content">The content to write.</param>
	/// <param name="isMultiline">Whether the input content is multiline.</param>
	public void WriteIf(bool condition, ReadOnlySpan<char> content, bool isMultiline = false)
	{
		if (condition)
		{
			Write(content, isMultiline);
		}
	}

	/// <summary>
	/// Writes content to the underlying buffer depending on an input condition.
	/// </summary>
	/// <param name="condition">The condition to use to decide whether or not to write content.</param>
	/// <param name="handler">The interpolated string handler with content to write.</param>
	public void WriteIf(bool condition, [InterpolatedStringHandlerArgument("", nameof(condition))] ref WriteIfInterpolatedStringHandler handler)
	{
		_ = this;
	}

	/// <summary>
	/// Writes a line to the underlying buffer.
	/// </summary>
	/// <param name="skipIfPresent">Indicates whether to skip adding the line if there already is one.</param>
	public void WriteLine(bool skipIfPresent = false)
	{
		if (skipIfPresent && _builder.WrittenSpan is [ .., '\n', '\n' ])
		{
			return;
		}

		_builder.Add(DefaultNewLine);
	}

	/// <summary>
	/// Writes content to the underlying buffer and appends a trailing new line.
	/// </summary>
	/// <param name="content">The content to write.</param>
	/// <param name="isMultiline">Whether the input content is multiline.</param>
	public void WriteLine(string content, bool isMultiline = false)
	{
		WriteLine(content.AsSpan(), isMultiline);
	}

	/// <summary>
	/// Writes content to the underlying buffer and appends a trailing new line.
	/// </summary>
	/// <param name="content">The content to write.</param>
	/// <param name="isMultiline">Whether the input content is multiline.</param>
	public void WriteLine(ReadOnlySpan<char> content, bool isMultiline = false)
	{
		Write(content, isMultiline);
		WriteLine();
	}

	/// <summary>
	/// Writes content to the underlying buffer and appends a trailing new line.
	/// </summary>
	/// <param name="handler">The interpolated string handler with content to write.</param>
	public void WriteLine([InterpolatedStringHandlerArgument("")] ref WriteInterpolatedStringHandler handler)
	{
		WriteLine();
	}

	/// <summary>
	/// Writes a line to the underlying buffer depending on an input condition.
	/// </summary>
	/// <param name="condition">The condition to use to decide whether or not to write content.</param>
	/// <param name="skipIfPresent">Indicates whether to skip adding the line if there already is one.</param>
	public void WriteLineIf(bool condition, bool skipIfPresent = false)
	{
		if (condition)
		{
			WriteLine(skipIfPresent);
		}
	}

	/// <summary>
	/// Writes content to the underlying buffer and appends a trailing new line depending on an input condition.
	/// </summary>
	/// <param name="condition">The condition to use to decide whether or not to write content.</param>
	/// <param name="content">The content to write.</param>
	/// <param name="isMultiline">Whether the input content is multiline.</param>
	public void WriteLineIf(bool condition, string content, bool isMultiline = false)
	{
		if (condition)
		{
			WriteLine(content.AsSpan(), isMultiline);
		}
	}

	/// <summary>
	/// Writes content to the underlying buffer and appends a trailing new line depending on an input condition.
	/// </summary>
	/// <param name="condition">The condition to use to decide whether or not to write content.</param>
	/// <param name="content">The content to write.</param>
	/// <param name="isMultiline">Whether the input content is multiline.</param>
	public void WriteLineIf(bool condition, ReadOnlySpan<char> content, bool isMultiline = false)
	{
		if (condition)
		{
			Write(content, isMultiline);
			WriteLine();
		}
	}

	/// <summary>
	/// Writes content to the underlying buffer and appends a trailing new line depending on an input condition.
	/// </summary>
	/// <param name="condition">The condition to use to decide whether or not to write content.</param>
	/// <param name="handler">The interpolated string handler with content to write.</param>
	public void WriteLineIf(bool condition, [InterpolatedStringHandlerArgument("", nameof(condition))] ref WriteIfInterpolatedStringHandler handler)
	{
		if (condition)
		{
			WriteLine();
		}
	}

	/// <inheritdoc/>
	public override string ToString()
	{
		return _builder.WrittenSpan.Trim().ToString();
	}

	/// <summary>
	/// Creates a <see cref="SourceText"/> instance from the current content of the writer.
	/// </summary>
	/// <returns>A <see cref="SourceText"/> instance containing the written content.</returns>
	public SourceText ToSourceText()
	{
		return SourceText.From(ToString(), Encoding.UTF8);
	}

	/// <inheritdoc/>
	public void Dispose()
	{
		_builder.Dispose();
	}

	/// <summary>
	/// Writes raw text to the underlying buffer, adding leading indentation if needed.
	/// </summary>
	/// <param name="content">The raw text to write.</param>
	private void WriteRawText(ReadOnlySpan<char> content)
	{
		if (_builder.Count == 0 || _builder.WrittenSpan[^1] == DefaultNewLine)
		{
			_builder.AddRange(_currentIndentation.AsSpan());
		}

		_builder.AddRange(content);
	}

	/// <summary>
	/// A delegate representing a callback to write data into an <see cref="IndentedCodeWriter"/> instance.
	/// </summary>
	/// <typeparam name="T">The type of data to use.</typeparam>
	/// <param name="value">The input data to use to write into <paramref name="writer"/>.</param>
	/// <param name="writer">The <see cref="IndentedCodeWriter"/> instance to write into.</param>
	public delegate void Callback<in T>(T value, IndentedCodeWriter writer);

	/// <summary>
	/// Represents an indented block that needs to be closed.
	/// </summary>
	/// <param name="writer">The input <see cref="IndentedCodeWriter"/> instance to wrap.</param>
	public struct Block(IndentedCodeWriter writer, string ending) : IDisposable
	{
		/// <summary>
		/// The <see cref="IndentedCodeWriter"/> instance to write to.
		/// </summary>
		private IndentedCodeWriter? _writer = writer;

		/// <inheritdoc/>
		public void Dispose()
		{
			var writer = _writer;

			_writer = null;

			if (writer is not null)
			{
				writer.DecreaseIndent();
				writer.WriteLine(ending);
			}
		}
	}

	/// <summary>
	/// Provides a handler used by the language compiler to append interpolated strings into <see cref="IndentedCodeWriter"/> instances.
	/// </summary>
	[EditorBrowsable(EditorBrowsableState.Never)]
	[InterpolatedStringHandler]
	public readonly ref struct WriteInterpolatedStringHandler
	{
		/// <summary>The associated <see cref="IndentedCodeWriter"/> to which to append.</summary>
		private readonly IndentedCodeWriter _writer;

		/// <summary>Creates a handler used to append an interpolated string into a <see cref="StringBuilder"/>.</summary>
		/// <param name="literalLength">The number of constant characters outside of interpolation expressions in the interpolated string.</param>
		/// <param name="formattedCount">The number of interpolation expressions in the interpolated string.</param>
		/// <param name="writer">The associated <see cref="IndentedCodeWriter"/> to which to append.</param>
		/// <remarks>This is intended to be called only by compiler-generated code. Arguments are not validated as they'd otherwise be for members intended to be used directly.</remarks>
		public WriteInterpolatedStringHandler(int literalLength, int formattedCount, IndentedCodeWriter writer)
		{
			_writer = writer;
		}

		/// <summary>Writes the specified string to the handler.</summary>
		/// <param name="value">The string to write.</param>
		public void AppendLiteral(string value)
		{
			_writer.Write(value);
		}

		/// <summary>Writes the specified value to the handler.</summary>
		/// <param name="value">The value to write.</param>
		public void AppendFormatted(string? value)
		{
			AppendFormatted<string?>(value);
		}

		/// <summary>Writes the specified character span to the handler.</summary>
		/// <param name="value">The span to write.</param>
		public void AppendFormatted(ReadOnlySpan<char> value)
		{
			_writer.Write(value);
		}

		/// <summary>Writes the specified value to the handler.</summary>
		/// <param name="value">The value to write.</param>
		/// <typeparam name="T">The type of the value to write.</typeparam>
		public void AppendFormatted<T>(T value)
		{
			if (value is not null)
			{
				_writer.Write(value.ToString());
			}
		}

		/// <summary>Writes the specified value to the handler.</summary>
		/// <param name="value">The value to write.</param>
		/// <param name="format">The format string.</param>
		/// <typeparam name="T">The type of the value to write.</typeparam>
		public void AppendFormatted<T>(T value, string? format)
		{
			if (value is IFormattable formattable)
			{
				_writer.Write(formattable.ToString(format, CultureInfo.InvariantCulture));
			}
			else if (value is not null)
			{
				_writer.Write(value.ToString());
			}
		}

		/// <summary>Writes the specified parameter name to the handler.</summary>
		/// <param name="parameter">The parameter symbol to write.</param>
		public void AppendFormatted(IParameterSymbol parameter)
		{
			_writer.Write(parameter.Name);
		}

		/// <summary>
		/// Writes the specified parameter names to the handler.
		/// </summary>
		/// <param name="parameters">The parameter symbols to write.</param>
		public void AppendFormatted(ImmutableArray<IParameterSymbol> parameters)
		{
			for (var i = 0; i < parameters.Length; i++)
			{
				_writer.Write(parameters[i].Name);

				if (i < parameters.Length - 1)
				{
					_writer.Write(", ");
				}
			}
		}
	}

	/// <summary>
	/// Provides a handler used by the language compiler to conditionally append interpolated strings into <see cref="IndentedCodeWriter"/> instances.
	/// </summary>
	[EditorBrowsable(EditorBrowsableState.Never)]
	[InterpolatedStringHandler]
	public readonly ref struct WriteIfInterpolatedStringHandler
	{
		/// <summary>The associated <see cref="WriteInterpolatedStringHandler"/> to use.</summary>
		private readonly WriteInterpolatedStringHandler _handler;

		/// <summary>Creates a handler used to append an interpolated string into a <see cref="StringBuilder"/>.</summary>
		/// <param name="literalLength">The number of constant characters outside of interpolation expressions in the interpolated string.</param>
		/// <param name="formattedCount">The number of interpolation expressions in the interpolated string.</param>
		/// <param name="writer">The associated <see cref="IndentedCodeWriter"/> to which to append.</param>
		/// <param name="condition">The condition to use to decide whether or not to write content.</param>
		/// <param name="shouldAppend">A value indicating whether formatting should proceed.</param>
		/// <remarks>This is intended to be called only by compiler-generated code. Arguments are not validated as they'd otherwise be for members intended to be used directly.</remarks>
		public WriteIfInterpolatedStringHandler(int literalLength, int formattedCount, IndentedCodeWriter writer, bool condition, out bool shouldAppend)
		{
			if (condition)
			{
				_handler = new WriteInterpolatedStringHandler(literalLength, formattedCount, writer);

				shouldAppend = true;
			}
			else
			{
				_handler = default;

				shouldAppend = false;
			}
		}

		/// <inheritdoc cref="WriteInterpolatedStringHandler.AppendLiteral(string)"/>
		public void AppendLiteral(string value)
		{
			_handler.AppendLiteral(value);
		}

		/// <inheritdoc cref="WriteInterpolatedStringHandler.AppendFormatted(string?)"/>
		public void AppendFormatted(string? value)
		{
			_handler.AppendFormatted(value);
		}

		/// <inheritdoc cref="WriteInterpolatedStringHandler.AppendFormatted(ReadOnlySpan{char})"/>
		public void AppendFormatted(ReadOnlySpan<char> value)
		{
			_handler.AppendFormatted(value);
		}

		/// <inheritdoc cref="WriteInterpolatedStringHandler.AppendFormatted{T}(T)"/>
		public void AppendFormatted<T>(T value)
		{
			_handler.AppendFormatted(value);
		}

		/// <inheritdoc cref="WriteInterpolatedStringHandler.AppendFormatted{T}(T, string?)"/>
		public void AppendFormatted<T>(T value, string? format)
		{
			_handler.AppendFormatted(value, format);
		}

		/// <inheritdoc cref="WriteInterpolatedStringHandler.AppendFormatted(IParameterSymbol)"/>
		public void AppendFormatted(IParameterSymbol parameter)
		{
			_handler.AppendFormatted(parameter);
		}

		/// <inheritdoc cref="WriteInterpolatedStringHandler.AppendFormatted(ImmutableArray{IParameterSymbol})"/>
		public void AppendFormatted(ImmutableArray<IParameterSymbol> parameters)
		{
			_handler.AppendFormatted(parameters);
		}
	}
}
