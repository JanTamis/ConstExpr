using System;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace SourceGen.Utilities.Helpers;

/// <summary>
///   Controls whether a statement that could be written without braces keeps them.
/// </summary>
public enum BracePreference
{
	/// <summary>Strip braces around single statements (<c>csharp_prefer_braces = false</c>).</summary>
	Never,

	/// <summary>Always keep/add braces (<c>csharp_prefer_braces = true</c>).</summary>
	Always,

	/// <summary>Only keep braces when the statement spans multiple lines.</summary>
	WhenMultiline
}

/// <summary>
///   The constructs that get their opening brace on a new line
///   (<c>csharp_new_line_before_open_brace</c>).
/// </summary>
[Flags]
public enum BraceNewLinePlacement
{
	None = 0,
	Accessors = 1 << 0,
	AnonymousMethods = 1 << 1,
	AnonymousTypes = 1 << 2,
	ControlBlocks = 1 << 3,
	Events = 1 << 4,
	Indexers = 1 << 5,
	Lambdas = 1 << 6,
	LocalFunctions = 1 << 7,
	Methods = 1 << 8,
	ObjectCollectionArrayInitializers = 1 << 9,
	Properties = 1 << 10,
	Types = 1 << 11,

	All = Accessors | AnonymousMethods | AnonymousTypes | ControlBlocks | Events | Indexers
	      | Lambdas | LocalFunctions | Methods | ObjectCollectionArrayInitializers | Properties | Types
}

/// <summary>
///   Controls whether a member is written with an expression body
///   (<c>csharp_style_expression_bodied_*</c>).
/// </summary>
public enum ExpressionBodyPreference
{
	/// <summary>Never use an expression body; convert to a block body instead.</summary>
	Never,

	/// <summary>Use an expression body whenever the generator is able to produce one.</summary>
	WhenPossible,

	/// <summary>Use an expression body only when the result stays on a single line.</summary>
	WhenOnSingleLine
}

/// <summary>
///   Controls whether namespaces are emitted file-scoped or block-scoped
///   (<c>csharp_style_namespace_declarations</c>).
/// </summary>
public enum NamespaceStyle
{
	FileScoped,
	BlockScoped
}

/// <summary>
///   Where using directives are placed (<c>csharp_using_directive_placement</c>).
/// </summary>
public enum UsingPlacement
{
	OutsideNamespace,
	InsideNamespace
}

/// <summary>
///   How <c>goto</c> labels are indented (<c>csharp_indent_labels</c>).
/// </summary>
public enum LabelIndentation
{
	/// <summary>Leave the label where the normalizer put it.</summary>
	NoChange,

	/// <summary>Move the label to column 0.</summary>
	FlushLeft,

	/// <summary>Move the label one level out from the surrounding statements.</summary>
	OneLessThanCurrent
}

/// <summary>
///   The parenthesised constructs that get a space just inside their parentheses
///   (<c>csharp_space_between_parentheses</c>).
/// </summary>
[Flags]
public enum ParenthesesSpacing
{
	None = 0,
	ControlFlowStatements = 1 << 0,
	Expressions = 1 << 1,
	TypeCasts = 1 << 2
}

/// <summary>
///   Spacing around binary operators (<c>csharp_space_around_binary_operators</c>).
/// </summary>
public enum BinaryOperatorSpacing
{
	BeforeAndAfter,
	None,

	/// <summary>Leave whatever spacing is already there.</summary>
	Ignore
}

/// <summary>
///   The formatting settings the generated code is rendered with, read from the
///   <c>.editorconfig</c> that applies to the project being compiled.
/// </summary>
/// <remarks>
///   <para>
///     A source generator can only observe <c>.editorconfig</c>; the IDE's own (non-exported)
///     formatting settings are invisible to the compiler. Rider/ReSharper does write its settings
///     to <c>.editorconfig</c>, so those arrive through the same mechanism.
///   </para>
///   <para>
///     <see cref="Default" /> deliberately reproduces the generator's historic hard-coded output:
///     tab indentation, CRLF, braces stripped around single statements, expression bodies enabled
///     and file-scoped namespaces. Every rendering phase that is new must be a no-op under
///     <see cref="Default" />, so code paths without an <c>.editorconfig</c> (notably the test
///     suite) keep producing byte-identical results.
///   </para>
/// </remarks>
public sealed record FormattingOptions
{
	/// <summary>
	///   The historic hard-coded behaviour. Also the fallback for every option that is absent
	///   or unparseable.
	/// </summary>
	public static readonly FormattingOptions Default = new();

	/// <summary>One level of indentation, e.g. <c>"\t"</c> or two spaces.</summary>
	public string IndentationString { get; init; } = "\t";

	/// <summary>The line separator, e.g. <c>"\r\n"</c>. Matches <c>NormalizeWhitespace</c>'s own default.</summary>
	public string EndOfLine { get; init; } = "\r\n";

	/// <summary>Whether the generated file ends with a line break.</summary>
	public bool InsertFinalNewline { get; init; }

	public BracePreference PreferBraces { get; init; } = BracePreference.Never;

	public BraceNewLinePlacement NewLineBeforeOpenBrace { get; init; } = BraceNewLinePlacement.All;

	public bool NewLineBeforeElse { get; init; } = true;

	public bool NewLineBeforeCatch { get; init; } = true;

	public bool NewLineBeforeFinally { get; init; } = true;

	public ExpressionBodyPreference ExpressionBodiedMethods { get; init; } = ExpressionBodyPreference.WhenPossible;

	public ExpressionBodyPreference ExpressionBodiedLocalFunctions { get; init; } = ExpressionBodyPreference.WhenPossible;

	public ExpressionBodyPreference ExpressionBodiedLambdas { get; init; } = ExpressionBodyPreference.WhenPossible;

	public ExpressionBodyPreference ExpressionBodiedProperties { get; init; } = ExpressionBodyPreference.WhenPossible;

	public ExpressionBodyPreference ExpressionBodiedIndexers { get; init; } = ExpressionBodyPreference.WhenPossible;

	public ExpressionBodyPreference ExpressionBodiedAccessors { get; init; } = ExpressionBodyPreference.WhenPossible;

	public ExpressionBodyPreference ExpressionBodiedConstructors { get; init; } = ExpressionBodyPreference.WhenPossible;

	public ExpressionBodyPreference ExpressionBodiedOperators { get; init; } = ExpressionBodyPreference.WhenPossible;

	public NamespaceStyle NamespaceDeclarations { get; init; } = NamespaceStyle.FileScoped;

	public bool SortSystemDirectivesFirst { get; init; } = true;

	public bool SeparateImportDirectiveGroups { get; init; }

	public UsingPlacement UsingDirectivePlacement { get; init; } = UsingPlacement.OutsideNamespace;

	public bool NewLineBeforeMembersInObjectInitializers { get; init; } = true;

	public bool NewLineBeforeMembersInAnonymousTypes { get; init; } = true;

	public bool NewLineBetweenQueryExpressionClauses { get; init; } = true;

	#region Indentation

	public bool IndentCaseContents { get; init; } = true;

	public bool IndentCaseContentsWhenBlock { get; init; } = true;

	public bool IndentSwitchLabels { get; init; } = true;

	public LabelIndentation IndentLabels { get; init; } = LabelIndentation.NoChange;

	public bool IndentBlockContents { get; init; } = true;

	public bool IndentBraces { get; init; }

	public bool PreserveSingleLineBlocks { get; init; } = true;

	public bool PreserveSingleLineStatements { get; init; } = true;

	#endregion

	#region Spacing

	public bool SpaceAfterCast { get; init; }

	public bool SpaceAfterKeywordsInControlFlowStatements { get; init; } = true;

	public ParenthesesSpacing SpaceBetweenParentheses { get; init; } = ParenthesesSpacing.None;

	public bool SpaceBeforeColonInInheritanceClause { get; init; } = true;

	public bool SpaceAfterColonInInheritanceClause { get; init; } = true;

	public BinaryOperatorSpacing SpaceAroundBinaryOperators { get; init; } = BinaryOperatorSpacing.BeforeAndAfter;

	public bool SpaceBetweenMethodDeclarationParameterListParentheses { get; init; }

	public bool SpaceBetweenMethodDeclarationEmptyParameterListParentheses { get; init; }

	public bool SpaceBetweenMethodDeclarationNameAndOpenParenthesis { get; init; }

	public bool SpaceBetweenMethodCallParameterListParentheses { get; init; }

	public bool SpaceBetweenMethodCallEmptyParameterListParentheses { get; init; }

	public bool SpaceBetweenMethodCallNameAndOpeningParenthesis { get; init; }

	public bool SpaceAfterComma { get; init; } = true;

	public bool SpaceBeforeComma { get; init; }

	public bool SpaceAfterDot { get; init; }

	public bool SpaceBeforeDot { get; init; }

	public bool SpaceAfterSemicolonInForStatement { get; init; } = true;

	public bool SpaceBeforeSemicolonInForStatement { get; init; }

	public bool SpaceBeforeOpenSquareBrackets { get; init; }

	public bool SpaceBetweenEmptySquareBrackets { get; init; }

	public bool SpaceBetweenSquareBrackets { get; init; }

	#endregion

	#region Output

	/// <summary>Whether the emitted file starts with a UTF-8 byte order mark (<c>charset</c>).</summary>
	public bool EmitByteOrderMark { get; init; } = true;

	public bool TrimTrailingWhitespace { get; init; }

	#endregion

	/// <summary>
	///   Reads the options that apply to <paramref name="tree" />. The whitespace and
	///   <c>csharp_*</c> keys live in per-file sections such as <c>[*.cs]</c>, so they are only
	///   reachable through <see cref="AnalyzerConfigOptionsProvider.GetOptions(SyntaxTree)" /> -
	///   <see cref="AnalyzerConfigOptionsProvider.GlobalOptions" /> only carries
	///   <c>build_property.*</c>.
	/// </summary>
	public static FormattingOptions Read(AnalyzerConfigOptionsProvider? provider, SyntaxTree? tree)
	{
		if (provider is null || tree is null)
		{
			return Default;
		}

		AnalyzerConfigOptions options;

		try
		{
			options = provider.GetOptions(tree);
		}
		catch (Exception)
		{
			return Default;
		}

		return new FormattingOptions
		{
			IndentationString = ReadIndentation(options),
			EndOfLine = ReadEndOfLine(options),
			InsertFinalNewline = ReadBool(options, "insert_final_newline", Default.InsertFinalNewline),
			PreferBraces = ReadPreferBraces(options),
			NewLineBeforeOpenBrace = ReadNewLineBeforeOpenBrace(options),
			NewLineBeforeElse = ReadBool(options, "csharp_new_line_before_else", Default.NewLineBeforeElse),
			NewLineBeforeCatch = ReadBool(options, "csharp_new_line_before_catch", Default.NewLineBeforeCatch),
			NewLineBeforeFinally = ReadBool(options, "csharp_new_line_before_finally", Default.NewLineBeforeFinally),
			ExpressionBodiedMethods = ReadExpressionBody(options, "csharp_style_expression_bodied_methods", Default.ExpressionBodiedMethods),
			ExpressionBodiedLocalFunctions = ReadExpressionBody(options, "csharp_style_expression_bodied_local_functions", Default.ExpressionBodiedLocalFunctions),
			ExpressionBodiedLambdas = ReadExpressionBody(options, "csharp_style_expression_bodied_lambdas", Default.ExpressionBodiedLambdas),
			ExpressionBodiedProperties = ReadExpressionBody(options, "csharp_style_expression_bodied_properties", Default.ExpressionBodiedProperties),
			ExpressionBodiedIndexers = ReadExpressionBody(options, "csharp_style_expression_bodied_indexers", Default.ExpressionBodiedIndexers),
			ExpressionBodiedAccessors = ReadExpressionBody(options, "csharp_style_expression_bodied_accessors", Default.ExpressionBodiedAccessors),
			ExpressionBodiedConstructors = ReadExpressionBody(options, "csharp_style_expression_bodied_constructors", Default.ExpressionBodiedConstructors),
			ExpressionBodiedOperators = ReadExpressionBody(options, "csharp_style_expression_bodied_operators", Default.ExpressionBodiedOperators),
			NamespaceDeclarations = ReadNamespaceStyle(options),
			SortSystemDirectivesFirst = ReadBool(options, "dotnet_sort_system_directives_first", Default.SortSystemDirectivesFirst),
			SeparateImportDirectiveGroups = ReadBool(options, "dotnet_separate_import_directive_groups", Default.SeparateImportDirectiveGroups),
			UsingDirectivePlacement = ReadValue(options, "csharp_using_directive_placement")?.ToLowerInvariant() switch
			{
				"inside_namespace" => UsingPlacement.InsideNamespace,
				"outside_namespace" => UsingPlacement.OutsideNamespace,
				_ => Default.UsingDirectivePlacement
			},
			NewLineBeforeMembersInObjectInitializers = ReadBool(options, "csharp_new_line_before_members_in_object_initializers", Default.NewLineBeforeMembersInObjectInitializers),
			NewLineBeforeMembersInAnonymousTypes = ReadBool(options, "csharp_new_line_before_members_in_anonymous_types", Default.NewLineBeforeMembersInAnonymousTypes),
			NewLineBetweenQueryExpressionClauses = ReadBool(options, "csharp_new_line_between_query_expression_clauses", Default.NewLineBetweenQueryExpressionClauses),

			IndentCaseContents = ReadBool(options, "csharp_indent_case_contents", Default.IndentCaseContents),
			IndentCaseContentsWhenBlock = ReadBool(options, "csharp_indent_case_contents_when_block", Default.IndentCaseContentsWhenBlock),
			IndentSwitchLabels = ReadBool(options, "csharp_indent_switch_labels", Default.IndentSwitchLabels),
			IndentLabels = ReadValue(options, "csharp_indent_labels")?.ToLowerInvariant() switch
			{
				"flush_left" => LabelIndentation.FlushLeft,
				"one_less_than_current" => LabelIndentation.OneLessThanCurrent,
				"no_change" => LabelIndentation.NoChange,
				_ => Default.IndentLabels
			},
			IndentBlockContents = ReadBool(options, "csharp_indent_block_contents", Default.IndentBlockContents),
			IndentBraces = ReadBool(options, "csharp_indent_braces", Default.IndentBraces),
			PreserveSingleLineBlocks = ReadBool(options, "csharp_preserve_single_line_blocks", Default.PreserveSingleLineBlocks),
			PreserveSingleLineStatements = ReadBool(options, "csharp_preserve_single_line_statements", Default.PreserveSingleLineStatements),

			SpaceAfterCast = ReadBool(options, "csharp_space_after_cast", Default.SpaceAfterCast),
			SpaceAfterKeywordsInControlFlowStatements = ReadBool(options, "csharp_space_after_keywords_in_control_flow_statements", Default.SpaceAfterKeywordsInControlFlowStatements),
			SpaceBetweenParentheses = ReadParenthesesSpacing(options),
			SpaceBeforeColonInInheritanceClause = ReadBool(options, "csharp_space_before_colon_in_inheritance_clause", Default.SpaceBeforeColonInInheritanceClause),
			SpaceAfterColonInInheritanceClause = ReadBool(options, "csharp_space_after_colon_in_inheritance_clause", Default.SpaceAfterColonInInheritanceClause),
			SpaceAroundBinaryOperators = ReadValue(options, "csharp_space_around_binary_operators")?.ToLowerInvariant() switch
			{
				"before_and_after" => BinaryOperatorSpacing.BeforeAndAfter,
				"none" => BinaryOperatorSpacing.None,
				"ignore" => BinaryOperatorSpacing.Ignore,
				_ => Default.SpaceAroundBinaryOperators
			},
			SpaceBetweenMethodDeclarationParameterListParentheses = ReadBool(options, "csharp_space_between_method_declaration_parameter_list_parentheses", Default.SpaceBetweenMethodDeclarationParameterListParentheses),
			SpaceBetweenMethodDeclarationEmptyParameterListParentheses = ReadBool(options, "csharp_space_between_method_declaration_empty_parameter_list_parentheses", Default.SpaceBetweenMethodDeclarationEmptyParameterListParentheses),
			SpaceBetweenMethodDeclarationNameAndOpenParenthesis = ReadBool(options, "csharp_space_between_method_declaration_name_and_open_parenthesis", Default.SpaceBetweenMethodDeclarationNameAndOpenParenthesis),
			SpaceBetweenMethodCallParameterListParentheses = ReadBool(options, "csharp_space_between_method_call_parameter_list_parentheses", Default.SpaceBetweenMethodCallParameterListParentheses),
			SpaceBetweenMethodCallEmptyParameterListParentheses = ReadBool(options, "csharp_space_between_method_call_empty_parameter_list_parentheses", Default.SpaceBetweenMethodCallEmptyParameterListParentheses),
			SpaceBetweenMethodCallNameAndOpeningParenthesis = ReadBool(options, "csharp_space_between_method_call_name_and_opening_parenthesis", Default.SpaceBetweenMethodCallNameAndOpeningParenthesis),
			SpaceAfterComma = ReadBool(options, "csharp_space_after_comma", Default.SpaceAfterComma),
			SpaceBeforeComma = ReadBool(options, "csharp_space_before_comma", Default.SpaceBeforeComma),
			SpaceAfterDot = ReadBool(options, "csharp_space_after_dot", Default.SpaceAfterDot),
			SpaceBeforeDot = ReadBool(options, "csharp_space_before_dot", Default.SpaceBeforeDot),
			SpaceAfterSemicolonInForStatement = ReadBool(options, "csharp_space_after_semicolon_in_for_statement", Default.SpaceAfterSemicolonInForStatement),
			SpaceBeforeSemicolonInForStatement = ReadBool(options, "csharp_space_before_semicolon_in_for_statement", Default.SpaceBeforeSemicolonInForStatement),
			SpaceBeforeOpenSquareBrackets = ReadBool(options, "csharp_space_before_open_square_brackets", Default.SpaceBeforeOpenSquareBrackets),
			SpaceBetweenEmptySquareBrackets = ReadBool(options, "csharp_space_between_empty_square_brackets", Default.SpaceBetweenEmptySquareBrackets),
			SpaceBetweenSquareBrackets = ReadBool(options, "csharp_space_between_square_brackets", Default.SpaceBetweenSquareBrackets),

			// charset: only the presence of a byte order mark is observable in the emitted file, so
			// utf-8 means "no BOM" and utf-8-bom (the compiler's own default) means "with BOM".
			EmitByteOrderMark = ReadValue(options, "charset")?.ToLowerInvariant() switch
			{
				"utf-8" => false,
				"utf-8-bom" => true,
				_ => Default.EmitByteOrderMark
			},
			TrimTrailingWhitespace = ReadBool(options, "trim_trailing_whitespace", Default.TrimTrailingWhitespace)
		};
	}

	private static ParenthesesSpacing ReadParenthesesSpacing(AnalyzerConfigOptions options)
	{
		var value = ReadValue(options, "csharp_space_between_parentheses");

		if (value is null)
		{
			return Default.SpaceBetweenParentheses;
		}

		var result = ParenthesesSpacing.None;

		foreach (var part in value.Split(','))
		{
			result |= part.Trim().ToLowerInvariant() switch
			{
				"control_flow_statements" => ParenthesesSpacing.ControlFlowStatements,
				"expressions" => ParenthesesSpacing.Expressions,
				"type_casts" => ParenthesesSpacing.TypeCasts,
				_ => ParenthesesSpacing.None
			};
		}

		return result;
	}

	/// <summary>
	///   Reads a raw value and strips the optional <c>:severity</c> suffix, so that
	///   <c>true:suggestion</c> reads as <c>true</c>.
	/// </summary>
	private static string? ReadValue(AnalyzerConfigOptions options, string key)
	{
		if (!options.TryGetValue(key, out var value) || String.IsNullOrWhiteSpace(value))
		{
			return null;
		}

		var separator = value.IndexOf(':');

		if (separator >= 0)
		{
			value = value.Substring(0, separator);
		}

		value = value.Trim();

		return value.Length == 0 ? null : value;
	}

	private static bool ReadBool(AnalyzerConfigOptions options, string key, bool fallback)
	{
		return ReadValue(options, key) switch
		{
			null => fallback,
			var value when String.Equals(value, "true", StringComparison.OrdinalIgnoreCase) => true,
			var value when String.Equals(value, "false", StringComparison.OrdinalIgnoreCase) => false,
			_ => fallback
		};
	}

	/// <summary>
	///   Builds one indentation level. When <c>indent_style</c> is absent the historic tab is kept
	///   and <c>indent_size</c> is deliberately ignored: without an explicit style the editor falls
	///   back to its own setting, which is not observable here, so changing the output would be a
	///   guess.
	/// </summary>
	private static string ReadIndentation(AnalyzerConfigOptions options)
	{
		var style = ReadValue(options, "indent_style");

		if (style is null || String.Equals(style, "tab", StringComparison.OrdinalIgnoreCase))
		{
			return Default.IndentationString;
		}

		if (!String.Equals(style, "space", StringComparison.OrdinalIgnoreCase))
		{
			return Default.IndentationString;
		}

		var size = ReadValue(options, "indent_size");

		// The editorconfig spec allows "indent_size = tab", which defers to tab_width.
		if (size is null || String.Equals(size, "tab", StringComparison.OrdinalIgnoreCase))
		{
			size = ReadValue(options, "tab_width");
		}

		if (size is null || !Int32.TryParse(size, out var width) || width is < 1 or > 32)
		{
			width = 4;
		}

		return new string(' ', width);
	}

	private static string ReadEndOfLine(AnalyzerConfigOptions options)
	{
		return ReadValue(options, "end_of_line")?.ToLowerInvariant() switch
		{
			"lf" => "\n",
			"crlf" => "\r\n",
			"cr" => "\r",
			_ => Default.EndOfLine
		};
	}

	private static BracePreference ReadPreferBraces(AnalyzerConfigOptions options)
	{
		return ReadValue(options, "csharp_prefer_braces")?.ToLowerInvariant() switch
		{
			"true" => BracePreference.Always,
			"false" => BracePreference.Never,
			"when_multiline" => BracePreference.WhenMultiline,
			_ => Default.PreferBraces
		};
	}

	private static ExpressionBodyPreference ReadExpressionBody(AnalyzerConfigOptions options, string key, ExpressionBodyPreference fallback)
	{
		return ReadValue(options, key)?.ToLowerInvariant() switch
		{
			"true" => ExpressionBodyPreference.WhenPossible,
			"false" => ExpressionBodyPreference.Never,
			"when_on_single_line" => ExpressionBodyPreference.WhenOnSingleLine,
			_ => fallback
		};
	}

	private static NamespaceStyle ReadNamespaceStyle(AnalyzerConfigOptions options)
	{
		return ReadValue(options, "csharp_style_namespace_declarations")?.ToLowerInvariant() switch
		{
			"file_scoped" => NamespaceStyle.FileScoped,
			"block_scoped" => NamespaceStyle.BlockScoped,
			_ => Default.NamespaceDeclarations
		};
	}

	private static BraceNewLinePlacement ReadNewLineBeforeOpenBrace(AnalyzerConfigOptions options)
	{
		var value = ReadValue(options, "csharp_new_line_before_open_brace");

		if (value is null)
		{
			return Default.NewLineBeforeOpenBrace;
		}

		if (String.Equals(value, "all", StringComparison.OrdinalIgnoreCase))
		{
			return BraceNewLinePlacement.All;
		}

		if (String.Equals(value, "none", StringComparison.OrdinalIgnoreCase))
		{
			return BraceNewLinePlacement.None;
		}

		var result = BraceNewLinePlacement.None;

		foreach (var part in value.Split(','))
		{
			result |= part.Trim().ToLowerInvariant() switch
			{
				"accessors" => BraceNewLinePlacement.Accessors,
				"anonymous_methods" => BraceNewLinePlacement.AnonymousMethods,
				"anonymous_types" => BraceNewLinePlacement.AnonymousTypes,
				"control_blocks" => BraceNewLinePlacement.ControlBlocks,
				"events" => BraceNewLinePlacement.Events,
				"indexers" => BraceNewLinePlacement.Indexers,
				"lambdas" => BraceNewLinePlacement.Lambdas,
				"local_functions" => BraceNewLinePlacement.LocalFunctions,
				"methods" => BraceNewLinePlacement.Methods,
				"object_collection_array_initializers" => BraceNewLinePlacement.ObjectCollectionArrayInitializers,
				"properties" => BraceNewLinePlacement.Properties,
				"types" => BraceNewLinePlacement.Types,
				_ => BraceNewLinePlacement.None
			};
		}

		return result;
	}
}