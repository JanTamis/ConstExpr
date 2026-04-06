// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Vectorize.ConstExpr.SourceGenerator.BuildIn;

/// <summary>Provides localized string resources for the regular expression engine.</summary>
internal static class SR
{
	public static string MakeException => "Invalid pattern '{0}' at offset {1}. {2}";
	public static string AlternationHasNamedCapture => "Alternation conditions do not capture and cannot be named.";
	public static string AlternationHasComment => "Alternation conditions cannot be comments.";
	public static string ShorthandClassInCharacterRange => "Cannot include class \\{0} in character range.";
	public static string QuantifierOrCaptureGroupOutOfRange => "Quantifier and capture group numbers must be less than or equal to Int32.MaxValue.";
	public static string InvalidUnicodePropertyEscape => "Incomplete \\p{X} character escape.";
	public static string MalformedUnicodePropertyEscape => "Malformed \\p{X} character escape.";
	public static string MissingControlCharacter => "Missing control character.";
	public static string NestedQuantifiersNotParenthesized => "Nested quantifier '{0}'.";
	public static string InsufficientClosingParentheses => "Not enough )'s.";
	public static string QuantifierAfterNothing => "Quantifier '{0}' following nothing.";
	public static string InsufficientOpeningParentheses => "Too many )'s.";
	public static string UndefinedNumberedReference => "Reference to undefined group number {0}.";
	public static string UndefinedNamedReference => "Reference to undefined group name '{0}'.";
	public static string AlternationHasUndefinedReference => "Conditional alternation refers to an undefined group number {0}.";
	public static string AlternationHasMalformedReference => "Conditional alternation is missing a closing parenthesis after the group number {0}.";
	public static string AlternationHasTooManyConditions => "Too many  in (?()).";
	public static string AlternationHasMalformedCondition => "Illegal conditional (?(...)) expression.";
	public static string UnrecognizedControlCharacter => "Unrecognized control character.";
	public static string UnrecognizedEscape => "Unrecognized escape sequence \\{0}.";
	public static string InvalidGroupingConstruct => "Unrecognized grouping construct.";
	public static string InsufficientOrInvalidHexDigits => "Insufficient or invalid hexadecimal digits.";
	public static string UnterminatedBracket => "Unterminated [] set.";
	public static string UnterminatedComment => "Unterminated (?#...) comment.";
	public static string ReversedCharacterRange => "[x-y] range in reverse order.";
	public static string ExclusionGroupNotLast => "A subtraction must be the last element in a character class.";
	public static string ReversedQuantifierRange => "Illegal {x,y} with x > y.";
	public static string CaptureGroupOfZero => "Capture number cannot be zero.";
	public static string UnescapedEndingBackslash => "Illegal \\ at end of pattern.";
	public static string MalformedNamedReference => "Malformed \\k<...> named back reference.";
	public static string CaptureGroupNameInvalid => "Invalid group name: Group names must begin with a word character.";
	public static string UnrecognizedUnicodeProperty => "Unknown property '{0}'.";

	public static string Format(string resourceFormat, object? p1)
	{
		return String.Format(resourceFormat, p1);
	}

	public static string Format(string resourceFormat, object? p1, object? p2)
	{
		return String.Format(resourceFormat, p1, p2);
	}

	public static string Format(string resourceFormat, object? p1, object? p2, object? p3)
	{
		return String.Format(resourceFormat, p1, p2, p3);
	}

	public static string Format(string resourceFormat, params object?[] args)
	{
		return String.Format(resourceFormat, args);
	}
}