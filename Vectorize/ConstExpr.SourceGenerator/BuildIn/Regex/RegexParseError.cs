// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Vectorize.ConstExpr.SourceGenerator.BuildIn;

/// <summary>
/// Specifies the detailed underlying reason why a <see cref="RegexParseException"/> is thrown when a
/// regular expression contains a parsing error.
/// </summary>
#if SYSTEM_TEXT_REGULAREXPRESSIONS
    public
#else
internal
#endif
	enum RegexParseError
{
	Unknown,
	AlternationHasTooManyConditions,
	AlternationHasMalformedCondition,
	InvalidUnicodePropertyEscape,
	MalformedUnicodePropertyEscape,
	UnrecognizedEscape,
	UnrecognizedControlCharacter,
	MissingControlCharacter,
	InsufficientOrInvalidHexDigits,
	QuantifierOrCaptureGroupOutOfRange,
	UndefinedNamedReference,
	UndefinedNumberedReference,
	MalformedNamedReference,
	UnescapedEndingBackslash,
	UnterminatedComment,
	InvalidGroupingConstruct,
	AlternationHasNamedCapture,
	AlternationHasComment,
	AlternationHasMalformedReference,
	AlternationHasUndefinedReference,
	CaptureGroupNameInvalid,
	CaptureGroupOfZero,
	UnterminatedBracket,
	ExclusionGroupNotLast,
	ReversedCharacterRange,
	ShorthandClassInCharacterRange,
	InsufficientClosingParentheses,
	ReversedQuantifierRange,
	NestedQuantifiersNotParenthesized,
	QuantifierAfterNothing,
	InsufficientOpeningParentheses,
	UnrecognizedUnicodeProperty
}