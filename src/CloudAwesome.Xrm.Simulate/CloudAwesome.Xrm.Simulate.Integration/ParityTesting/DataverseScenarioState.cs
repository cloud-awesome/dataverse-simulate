using System;
using System.Collections.Generic;

namespace CloudAwesome.Xrm.Simulate.Gather.ParityTesting;

public sealed class DataverseScenarioState
{
    private readonly Dictionary<string, object?> _items = new(StringComparer.Ordinal);

    public void Set<T>(string key, T value)
    {
        _items[key] = value;
    }

    public T Get<T>(string key)
    {
        if (!_items.TryGetValue(key, out var value))
        {
            throw new KeyNotFoundException($"Scenario state does not contain an item named '{key}'.");
        }

        return value is T typed
            ? typed
            : throw new InvalidCastException(
                $"Scenario state item '{key}' is '{value?.GetType().Name ?? "null"}', not '{typeof(T).Name}'.");
    }
}
