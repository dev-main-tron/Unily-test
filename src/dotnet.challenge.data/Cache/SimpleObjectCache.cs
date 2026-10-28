using System.Collections.Concurrent;

namespace dotnet.challenge.data.Cache;

/// <summary>
/// In-memory object store. Thread-safe: the service is a singleton hit by concurrent requests,
/// so the backing map must tolerate parallel readers and writers.
/// </summary>
/// <remarks>
/// Thread-safety here protects the <em>map</em> (no corruption, no lost keys). It does not make
/// read-modify-write sequences on the stored objects atomic; that is the caller's job
/// (see <c>InventoryService</c>, which serialises stock changes per product).
/// </remarks>
public class SimpleObjectCache<TKey, TValue> : ISimpleObjectCache<TKey, TValue>
    where TKey : struct
    where TValue : class
{
    private readonly ConcurrentDictionary<TKey, TValue> _store = new();

    public Task AddAsync(TKey key, TValue value)
    {
        _store[key] = value;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(TKey key)
    {
        _store.TryRemove(key, out _);
        return Task.CompletedTask;
    }

    public Task<TValue?> GetAsync(TKey key)
    {
        _store.TryGetValue(key, out var value);
        return Task.FromResult(value);
    }

    public Task<IEnumerable<TValue>> GetAllAsync()
    {
        IEnumerable<TValue> values = _store.Values.ToList();
        return Task.FromResult(values);
    }

    public Task UpdateAsync(TKey key, TValue value)
    {
        // Atomic "replace only if present", matching the original semantics without the
        // ContainsKey/indexer race.
        if (_store.TryGetValue(key, out var current))
        {
            _store.TryUpdate(key, value, current);
        }
        return Task.CompletedTask;
    }
}
