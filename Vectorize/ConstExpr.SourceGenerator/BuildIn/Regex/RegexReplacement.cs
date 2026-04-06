// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

namespace Vectorize.ConstExpr.SourceGenerator.BuildIn;

/// <summary>
/// The RegexReplacement class represents a substitution string for
/// use when using regexes to search/replace, etc. It's logically
/// a sequence intermixed (1) constant strings and (2) group numbers.
/// </summary>
public sealed class RegexReplacement
{
	// Constants for special insertion patterns
	private const int Specials = 4;
	public const int LeftPortion = -1;
	public const int RightPortion = -2;
	public const int LastGroup = -3;
	public const int WholeString = -4;

	private readonly string[] _strings; // table of string constants
	private readonly int[] _rules; // negative -> group #, positive -> string #
	private readonly bool _hasBackreferences; // true if the replacement has any backreferences; otherwise, false

	/// <summary>
	/// Since RegexReplacement shares the same parser as Regex,
	/// the constructor takes a RegexNode which is a concatenation
	/// of constant strings and backreferences.
	/// </summary>
	public RegexReplacement(string rep, RegexNode concat, Hashtable _caps)
	{
		Debug.Assert(concat.Kind == RegexNodeKind.Concatenate, $"Expected Concatenate, got {concat.Kind}");

		var sb = new StringBuilder();
		var strings = new List<string>();
		var rules = new List<int>();

		var childCount = concat.ChildCount();

		for (var i = 0; i < childCount; i++)
		{
			var child = concat.Child(i);

			switch (child.Kind)
			{
				case RegexNodeKind.Multi:
					sb.Append(child.Str!);
					break;

				case RegexNodeKind.One:
					sb.Append(child.Ch);
					break;

				case RegexNodeKind.Backreference:
					if (sb.Length > 0)
					{
						rules.Add(strings.Count);
						strings.Add(sb.ToString());
						sb.Clear();
					}
					var slot = child.M;

					if (_caps != null && slot >= 0)
					{
						slot = (int) _caps[slot]!;
					}

					rules.Add(-Specials - 1 - slot);
					_hasBackreferences = true;
					break;

				default:
					Debug.Fail($"Unexpected child kind {child.Kind}");
					break;
			}
		}

		if (sb.Length > 0)
		{
			rules.Add(strings.Count);
			strings.Add(sb.ToString());
		}

		Pattern = rep;
		_strings = strings.ToArray();
		_rules = rules.ToArray();
	}

	/// <summary>
	/// Either returns a weakly cached RegexReplacement helper or creates one and caches it.
	/// </summary>
	public static RegexReplacement GetOrCreate(WeakReference<RegexReplacement?> replRef, string replacement, Hashtable caps,
	                                           int capsize, Hashtable capnames, RegexOptions roptions)
	{
		if (!replRef.TryGetTarget(out var repl) || !repl.Pattern.Equals(replacement))
		{
			repl = RegexParser.ParseReplacement(replacement, roptions, caps, capsize, capnames);
			replRef.SetTarget(repl);
		}

		return repl;
	}

	/// <summary>The original pattern string</summary>
	public string Pattern { get; }

	/// <summary>
	/// Replaces all occurrences of the regex in the string with the
	/// replacement pattern.
	/// </summary>
	public string Replace(Regex regex, string input, int count, int startat)
	{
		if (count < -1)
			throw new ArgumentOutOfRangeException(nameof(count), "Count cannot be less than -1.");
		if ((uint) startat > (uint) input.Length)
			throw new ArgumentOutOfRangeException(nameof(startat), "startat must be non-negative.");

		if (count == 0)
			return input;

		var m = regex.Match(input, startat);
		if (!m.Success)
			return input;

		var sb = new StringBuilder();
		var prevIndex = startat;
		var matchCount = 0;

		while (m.Success)
		{
			sb.Append(input, prevIndex, m.Index - prevIndex);
			sb.Append(EvaluateReplacement(m, input));
			prevIndex = m.Index + m.Length;

			if (count != -1 && ++matchCount >= count)
				break;

			m = m.NextMatch();
		}

		sb.Append(input, prevIndex, input.Length - prevIndex);
		return sb.ToString();
	}

	/// <summary>Evaluates the replacement string for a given match.</summary>
	private string EvaluateReplacement(Match match, string input)
	{
		var sb = new StringBuilder();

		foreach (var rule in _rules)
		{
			if (rule >= 0)
			{
				// String literal lookup
				sb.Append(_strings[rule]);
			}
			else if (rule < -Specials)
			{
				// Group back-reference: group number = -Specials - 1 - rule
				var groupIndex = -Specials - 1 - rule;
				if (groupIndex < match.Groups.Count)
					sb.Append(match.Groups[groupIndex].Value);
			}
			else
			{
				// Special insertion pattern
				switch (-Specials - 1 - rule)
				{
					case LeftPortion:
						// $` — text before the match
						sb.Append(input, 0, match.Index);
						break;
					case RightPortion:
						// $' — text after the match
						sb.Append(input, match.Index + match.Length, input.Length - match.Index - match.Length);
						break;
					case LastGroup:
						// $+ — last successfully captured group
						var lastSuccessful = -1;

						for (var i = 1; i < match.Groups.Count; i++)
						{
							if (match.Groups[i].Success)
								lastSuccessful = i;
						}
						if (lastSuccessful >= 0)
							sb.Append(match.Groups[lastSuccessful].Value);
						break;
					case WholeString:
						// $_ — entire input string
						sb.Append(input);
						break;
				}
			}
		}

		return sb.ToString();
	}
}