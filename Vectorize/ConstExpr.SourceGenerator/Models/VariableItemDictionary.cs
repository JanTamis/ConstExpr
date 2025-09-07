using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace ConstExpr.SourceGenerator.Visitors;

public class VariableItemDictionary(IDictionary<string, VariableItem> inner) : IDictionary<string, object?>
{
	public bool TryGetValue(string key, [UnscopedRef] out object? value)
	{
		if (inner.TryGetValue(key, out var item) && item.HasValue)
		{
			value = item.Value;
			return true;
		}

		value = null;
		return false;
	}

	public object? this[string key]
	{
		get => inner[key].Value;
		set
		{
			if (inner.ContainsKey(key))
			{
				var item = inner[key];
				inner[key] = new VariableItem(item.Type, value is not null, value);
			}
			else
			{
				throw new KeyNotFoundException($"The given key '{key}' was not present in the dictionary.");
			}
		}
	}

	public ICollection<string> Keys => inner.Keys;

	public ICollection<object?> Values => inner.Values
		.Where(w => w.HasValue)
		.Select(v => v.Value)
		.ToList();

	public bool Remove(KeyValuePair<string, object?> item)
	{
		throw new NotSupportedException("Removing keys is not supported.");
	}

	public int Count => inner.Count(c => c.Value.HasValue);

	public bool IsReadOnly => inner.IsReadOnly;

	public void Add(string key, object? value)
	{
		throw new NotSupportedException("Adding new keys is not supported.");
	}

	public void Add(KeyValuePair<string, object?> item)
	{
		throw new NotSupportedException("Adding new keys is not supported.");
	}

	public void Clear()
	{
		throw new NotSupportedException("Clearing the dictionary is not supported.");
	}

	public bool Contains(KeyValuePair<string, object?> item)
	{
		return inner.TryGetValue(item.Key, out var value) && value.HasValue && Equals(value.Value, item.Value);
	}

	public bool ContainsKey(string key)
	{
		return inner.TryGetValue(key, out var item) && item.HasValue;
	}

	public void CopyTo(KeyValuePair<string, object?>[] array, int arrayIndex)
	{
		foreach (var kvp in inner)
		{
			if (kvp.Value.HasValue)
			{
				array[arrayIndex++] = new KeyValuePair<string, object?>(kvp.Key, kvp.Value.Value);
			}
		}
	}

	public IEnumerator<KeyValuePair<string, object?>> GetEnumerator()
	{
		foreach (var kvp in inner)
		{
			if (kvp.Value.HasValue)
			{
				yield return new KeyValuePair<string, object?>(kvp.Key, kvp.Value.Value);
			}
		}
	}

	public bool Remove(string key)
	{
		throw new NotSupportedException("Removing keys is not supported.");
	}

	IEnumerator IEnumerable.GetEnumerator()
	{
		return GetEnumerator();
	}
}