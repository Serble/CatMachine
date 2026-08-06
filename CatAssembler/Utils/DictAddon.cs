using System.Collections;
using System.Diagnostics.CodeAnalysis;

namespace CatAssembler.Utils;

public class DictAddon<TKey, TVal>(IDictionary<TKey, TVal> baseDict, KeyValuePair<TKey, TVal> additional) : IDictionary<TKey, TVal> {

    public IEnumerator<KeyValuePair<TKey, TVal>> GetEnumerator() {
        foreach (var val in baseDict) {
            yield return val;
        }

        yield return additional;
    }

    IEnumerator IEnumerable.GetEnumerator() {
        return GetEnumerator();
    }

    public void Add(KeyValuePair<TKey, TVal> item) {
        throw new NotSupportedException();
    }

    public void Clear() {
        throw new NotSupportedException();
    }

    public bool Contains(KeyValuePair<TKey, TVal> item) {
        return (Equals(item.Key, additional.Key) && Equals(item.Value, additional.Value)) || baseDict.Contains(item);
    }

    public void CopyTo(KeyValuePair<TKey, TVal>[] array, int arrayIndex) {
        throw new NotSupportedException();
    }

    public bool Remove(KeyValuePair<TKey, TVal> item) {
        throw new NotSupportedException();
    }

    public int Count => baseDict.Count + 1;
    public bool IsReadOnly => true;

    public void Add(TKey key, TVal value) {
        throw new NotSupportedException();
    }

    public bool ContainsKey(TKey key) {
        return Equals(additional.Key, key) || baseDict.ContainsKey(key);
    }

    public bool Remove(TKey key) {
        throw new NotSupportedException();
    }

    public bool TryGetValue(TKey key, [MaybeNullWhen(false)] out TVal value) {
        if (!ContainsKey(key)) {
            value = default;
            return false;
        }

        value = this[key];
        return true;
    }

    public TVal this[TKey key] {
        get => Equals(key, additional.Key) ? additional.Value : baseDict[key];
        set => throw new NotSupportedException();
    }

    public ICollection<TKey> Keys => [.. baseDict.Keys, additional.Key];
    public ICollection<TVal> Values => [.. baseDict.Values, additional.Value];
}
