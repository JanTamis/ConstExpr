namespace ConstExpr.SourceGenerator.Comparers;

// public class InvocationComparer : IEqualityComparer<InvocationModel?>
// {
// 	public bool Equals(InvocationModel? x, InvocationModel? y)
// 	{
// 		if (ReferenceEquals(x, y)) return true;
// 		if (x is null || y is null) return false;
//
// 		return x.Method.IsEquivalentTo(y.Method)
// 			&& x.Invocation.IsEquivalentTo(y.Invocation)
// 			&& EqualityComparer<object?>.Default.Equals(x.Value, y.Value);
// 	}
//
// 	public int GetHashCode(InvocationModel? obj)
// 	{
// 		if (obj is null) return 0;
//
// 		// Use a simplified hash that focuses on the content rather than object identity
// 		unchecked
// 		{
// 			int hash = 17;
// 			hash = hash * 31 + (obj.Method.ToString()?.GetHashCode() ?? 0);
// 			hash = hash * 31 + (obj.Invocation.ToString()?.GetHashCode() ?? 0);
// 			hash = hash * 31 + EqualityComparer<object?>.Default.GetHashCode(obj.Value);
// 			return hash;
// 		}
// 	}
// }
