// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections;
using System.Diagnostics;
using System.Globalization;

namespace Vectorize.ConstExpr.SourceGenerator.BuildIn;

public sealed class RegexTree
{
	public readonly RegexOptions Options;
	public readonly RegexNode Root;
	public readonly RegexFindOptimizations FindOptimizations;
	public readonly int CaptureCount;
	public readonly CultureInfo? Culture;
	public readonly string[]? CaptureNames;
	public readonly Hashtable? CaptureNameToNumberMapping;
	public readonly Hashtable? CaptureNumberSparseMapping;

	internal RegexTree(RegexNode root, int captureCount, string[]? captureNames, Hashtable? captureNameToNumberMapping, Hashtable? captureNumberSparseMapping, RegexOptions options, CultureInfo? culture)
	{
		Root = root;
		Culture = culture;
		CaptureNumberSparseMapping = captureNumberSparseMapping;
		CaptureCount = captureCount;
		CaptureNameToNumberMapping = captureNameToNumberMapping;
		CaptureNames = captureNames;
		Options = options;
		FindOptimizations = RegexFindOptimizations.Create(root, options);
	}
}