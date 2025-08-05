using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace ConstExpr.SourceGenerator.Comparers;

public class ValueOrCollectionEqualityComparer : IEqualityComparer<object?>
{
	public bool Equals(object? x, object? y)
	{
		if (ReferenceEquals(x, y))
		{
			return true;
		}

		if (x is null || y is null)
		{
			return false;
		}

		// Handle strings as values, not collections
		if (x is string || y is string)
		{
			return x.Equals(y);
		}

		// Both must be enumerable and of compatible types
		if (x is IEnumerable xEnum && y is IEnumerable yEnum &&
		    x.GetType() == y.GetType())
		{
			return xEnum.Cast<object?>().SequenceEqual(yEnum.Cast<object?>(), this);
		}

		// Fall back to default equality
		return x.Equals(y);
	}

	public int GetHashCode(object? obj)
	{
		if (obj is null)
		{
			return 0;
		}

		// Handle strings as values
		if (obj is string str)
		{
			return str.GetHashCode();
		}

		if (obj is IEnumerable enumerable)
		{
			unchecked
			{
				var hash = 19;

				foreach (var item in enumerable)
				{
					hash = hash * 31 + GetHashCode(item);
				}

				return hash;
			}
		}

		return obj.GetHashCode();
	}
}