// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Runtime.CompilerServices;

namespace Vectorize.ConstExpr.SourceGenerator.BuildIn;

/// <summary>Provides tools for avoiding stack overflows.</summary>
internal static class StackHelper
{
	/// <summary>Tries to ensure there is sufficient stack to execute the average .NET function.</summary>
	public static bool TryEnsureSufficientExecutionStack()
	{
		try
		{
			RuntimeHelpers.EnsureSufficientExecutionStack();
			return true;
		}
		catch
		{
			return false;
		}
	}

	/// <summary>Calls the provided action on a new thread with a larger stack to avoid stack overflow.</summary>
	public static void CallOnEmptyStack<T1, T2, T3>(Action<T1, T2, T3> action, T1 arg1, T2 arg2, T3 arg3)
	{
		action(arg1, arg2, arg3);
	}
}