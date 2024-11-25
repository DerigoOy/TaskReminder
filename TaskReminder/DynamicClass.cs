using System;
using System.Collections;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace TaskReminder
{
    [Serializable]
    public class DynamicClass : DynamicObject, ISerializable, IDictionary<string, object>
    {
        private Dictionary<string, object> innerDictionary = new Dictionary<string, object>();

        public DynamicClass()
        {
            innerDictionary = new Dictionary<string, object>();
        }

        public int ID { get; set; }

        // Implement ISerializable
        public void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            foreach (var pair in innerDictionary)
            {
                info.AddValue(pair.Key, pair.Value);
            }
        }

        // Custom deserialization constructor
        protected DynamicClass(SerializationInfo info, StreamingContext context)
        {
            // Initialize the object by deserializing values from SerializationInfo
            foreach (SerializationEntry entry in info)
            {
                innerDictionary[entry.Name] = entry.Value;
            }
        }

        // Implement IDictionary<string, object>
        public void Add(string key, object value)
        {
            if (!innerDictionary.ContainsKey(key))
            {
                innerDictionary.Add(key, value);
            }
            else { innerDictionary[key] = value; }
        }

        public bool ContainsKey(string key)
        {
            return innerDictionary.ContainsKey(key);
        }

        public ICollection<string> Keys => innerDictionary.Keys;

        public bool Remove(string key)
        {
            return innerDictionary.Remove(key);
        }

        public bool TryGetValue(string key, out object value)
        {
            return innerDictionary.TryGetValue(key, out value);
        }

        public ICollection<object> Values => innerDictionary.Values;

        public object this[string key]
        {
            get
            {
                if (innerDictionary.TryGetValue(key, out object value))
                {
                    return value;
                }
                throw new KeyNotFoundException($"The key '{key}' was not found.");
            }
            set
            {
                innerDictionary[key] = value;
            }
        }

        public void Add(KeyValuePair<string, object> item)
        {
            ((IDictionary<string, object>)innerDictionary).Add(item);
        }

        public void Clear()
        {
            innerDictionary.Clear();
        }

        public string GetString(string item)
        {
            if (innerDictionary.Keys.Contains(item))
            {
                return innerDictionary[item] + "";
            }
            return "";
        }

        public int? GetInteger(string item)
        {
            if (innerDictionary.Keys.Contains(item))
            {
                return (int?)innerDictionary[item];
            }
            return null;
        }

        public DateTime GetDateTime(string item){
            DateTime date;
            string value = innerDictionary.Keys.Contains(item) ? innerDictionary[item] + "" : null;
            bool success = DateTime.TryParse(value, out date);
            return success ? date : DateTime.MinValue;
        }


        public string GetFormattedDate(string item)
        {
            DateTime date;
            string value = innerDictionary.Keys.Contains(item) ? innerDictionary[item] + "" : null;
            bool success = DateTime.TryParse(value, out date);
            return success ? date.ToShortDateString() : "";
        }

        public bool Contains(string item)
        {
            return innerDictionary.Keys.Contains(item);
        }

        public bool Contains(KeyValuePair<string, object> item)
        {
            return ((IDictionary<string, object>)innerDictionary).Contains(item);
        }

        public void CopyTo(KeyValuePair<string, object>[] array, int arrayIndex)
        {
            ((IDictionary<string, object>)innerDictionary).CopyTo(array, arrayIndex);
        }

        public int Count => innerDictionary.Count;

        public bool IsReadOnly => ((IDictionary<string, object>)innerDictionary).IsReadOnly;
                
        public int? ProjectID { get; internal set; }

        public bool Remove(KeyValuePair<string, object> item)
        {
            return ((IDictionary<string, object>)innerDictionary).Remove(item);
        }

        public IEnumerator<KeyValuePair<string, object>> GetEnumerator()
        {
            return innerDictionary.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public DynamicClass Clone()
        {
            DynamicClass clone = new DynamicClass();
            foreach (var pair in innerDictionary)
            {
                clone.Add(pair.Key, pair.Value);
            }
            return clone;
        }

        // Implement DynamicObject
        public override bool TryGetMember(GetMemberBinder binder, out object result)
        {
            string name = binder.Name;
            return innerDictionary.TryGetValue(name, out result);
        }

        public override bool TrySetMember(SetMemberBinder binder, object value)
        {
            string name = binder.Name;
            innerDictionary[name] = value;
            return true;
        }
    }
}
