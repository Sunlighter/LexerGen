using Sunlighter.OptionLib;
using Sunlighter.TypeTraitsLib;
using System;

namespace Sunlighter.LexerGenLib
{
    public interface ICacheStorage
    {
        Option<byte[]> TryGet();

        void Set(byte[] value);
    }

    public static partial class Extensions
    {
        public static V GetCachedValue<K, V>
        (
            this ICacheStorage storage,
            ITypeTraits<K> keyTraits,
            ITypeTraits<V> valueTraits,
            K key,
            Func<K, V> calculate
        )
        {
            byte[] keyHash = keyTraits.GetSHA256Hash(key);
            Option<byte[]> cacheBlob = storage.TryGet();
            if (cacheBlob.HasValue)
            {
                byte[] cacheBytes = cacheBlob.Value;
                byte[] cacheHash = new byte[keyHash.Length];
                Array.Copy(cacheBytes, 0, cacheHash, 0, keyHash.Length);
                if (ByteArrayTypeTraits.Value.Compare(keyHash, cacheHash) == 0)
                {
                    byte[] cacheValue = new byte[cacheBytes.Length - keyHash.Length];
                    Array.Copy(cacheBytes, keyHash.Length, cacheValue, 0, cacheValue.Length);
                    return valueTraits.DeserializeFromBytes(cacheValue);
                }
            }
            V value = calculate(key);
            byte[] valueBytes = valueTraits.SerializeToBytes(value);
            byte[] cacheBlob2 = new byte[keyHash.Length + valueBytes.Length];
            Array.Copy(keyHash, 0, cacheBlob2, 0, keyHash.Length);
            Array.Copy(valueBytes, 0, cacheBlob2, keyHash.Length, valueBytes.Length);
            storage.Set(cacheBlob2);
            return value;
        }
    }
}
