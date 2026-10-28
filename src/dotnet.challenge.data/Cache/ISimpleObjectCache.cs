namespace dotnet.challenge.data.Cache;

public interface ISimpleObjectCache<TKey, TValue>
    where TKey : struct
    where TValue : class
{
    Task AddAsync(TKey key, TValue value);

    Task DeleteAsync(TKey key);

    Task<TValue?> GetAsync(TKey key);

    Task<IEnumerable<TValue>> GetAllAsync();

    Task UpdateAsync(TKey key, TValue value);
}
