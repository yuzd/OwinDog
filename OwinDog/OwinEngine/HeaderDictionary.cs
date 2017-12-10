using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace OwinEngine
{
    internal class HeaderDictionary : IDictionary<string, string[]>, ICollection<KeyValuePair<string, string[]>>, IEnumerable<KeyValuePair<string, string[]>>, IEnumerable
    {
        private readonly Dictionary<string, string[]> Headers;

        public HeaderDictionary()
        {
            Headers = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
        }

        public void Add(KeyValuePair<string, string[]> item)
        {
            Headers.Add(item.Key, item.Value);
        }

        public void Add(string key, string[] value)
        {
            Headers.Add(key, value);
        }

        public void Clear()
        {
            Headers.Clear();
        }

        public bool Contains(KeyValuePair<string, string[]> item)
        {
            return ((ICollection<KeyValuePair<string, string[]>>)Headers).Contains(item);
        }

        public bool ContainsKey(string key)
        {
            return Headers.ContainsKey(key);
        }

        public void CopyTo(KeyValuePair<string, string[]>[] array, int arrayIndex)
        {
            ((ICollection<KeyValuePair<string, string[]>>)Headers).CopyTo(array, arrayIndex);
        }

        private static string[] CreateArrayCopy(string[] original)
        {
            string[] array = new string[original.Length];
            Array.Copy(original, array, original.Length);
            return array;
        }

        public IEnumerator<KeyValuePair<string, string[]>> GetEnumerator()
        {
            return ((IEnumerable<KeyValuePair<string, string[]>>)Headers).GetEnumerator();
        }

        public bool Remove(string key)
        {
            return Headers.Remove(key);
        }

        public bool Remove(KeyValuePair<string, string[]> item)
        {
            return ((ICollection<KeyValuePair<string, string[]>>)Headers).Remove(item);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public bool TryGetValue(string key, out string[] value)
        {
            string[] original;
            if (Headers.TryGetValue(key, out original))
            {
                value = CreateArrayCopy(original);
                return true;
            }
            value = null;
            return false;
        }

        public int Count
        {
            get
            {
                return Headers.Count;
            }
        }

        public bool IsReadOnly
        {
            get
            {
                return false;
            }
        }

        public string[] this[string key]
        {
            get
            {
                return CreateArrayCopy(Headers[key]);
            }
            set
            {
                Headers[key] = value;
            }
        }

        public ICollection<string> Keys
        {
            get
            {
                return Headers.Keys;
            }
        }

        public ICollection<string[]> Values
        {
            get
            {
                List<string[]> list = Headers.Values.ToList<string[]>();
                checked
                {
                    for (int i = 0; i < list.Count; i++)
                    {
                        string[] array = list[i];
                        list[i] = new string[]
                        {
                            array[0]
                        };
                    }
                    return list;
                }
            }
        }


    }
}
