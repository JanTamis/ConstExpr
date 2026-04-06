// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.ComponentModel;
using System.Runtime.Serialization;

namespace Vectorize.ConstExpr.SourceGenerator.BuildIn;
#if SYSTEM_TEXT_REGULAREXPRESSIONS
    public
#else
internal
#endif
	sealed class RegexParseException : ArgumentException
{
	public RegexParseError Error { get; }
	public int Offset { get; }

	internal RegexParseException(RegexParseError error, int offset, string message) : base(message)
	{
		Error = error;
		Offset = offset;
	}

#if NET
        [Obsolete(Obsoletions.LegacyFormatterImplMessage, DiagnosticId = Obsoletions.LegacyFormatterImplDiagId, UrlFormat = Obsoletions.SharedUrlFormat)]
        [EditorBrowsable(EditorBrowsableState.Never)]
#endif
	public override void GetObjectData(SerializationInfo info, StreamingContext context)
	{
		base.GetObjectData(info, context);
		info.SetType(typeof(ArgumentException));
	}
}