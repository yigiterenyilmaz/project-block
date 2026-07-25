// PURPOSE: Save/restore the internal state of the CONTENT types - the 39 jokers, 30 powers
// and 11 bosses - without a hand-written serializer for each of them.
//
// WHY REFLECTION. Those 80 classes hold roughly a hundred private fields between them
// (streaks, counters, marked cells, accrued value, infection maps). Hand-writing a Save/Load
// pair for each would be 80 chances to forget one, and every future joker would silently lose
// its state until someone remembered to add its serializer. Walking the fields means new
// content saves correctly the day it is written, with no extra work and nothing to forget.
//
// DETERMINISTIC ORDER. Reflection does NOT promise a stable field order, so fields are sorted
// by name inside each type and the type hierarchy is walked base-class-first. Same order every
// run, on every runtime.
//
// WHAT IS SUPPORTED: primitives, strings, enums, Nullable<T>, List<T>/HashSet<T>/arrays,
// Dictionary<K,V>, and any struct (walked field by field, which is how GridPos, CubeAttachment
// and the small per-joker structs travel). A field holding a reference to another CONTENT
// object is supported only for Power, because exactly one exists ("Halüsinasyon" wearing the
// face of another power). Anything else throws by design - a new field shape should stop the
// build's tests, not quietly vanish from saves.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

namespace ProjectBlock.Core
{
    /// <summary>Reflection-driven save/load for jokers, powers and bosses.</summary>
    public static class ContentStateSerializer
    {
        private const BindingFlags DeclaredInstance = BindingFlags.Instance
            | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

        public static void Save(SaveWriter w, string key, object instance)
        {
            List<FieldInfo> fields = FieldsOf(instance.GetType());
            w.Write(key + ".fields", fields.Count);
            for (int i = 0; i < fields.Count; i++)
            {
                WriteValue(w, key + "." + fields[i].Name, fields[i].FieldType,
                    fields[i].GetValue(instance));
            }
        }

        public static void Load(SaveReader r, string key, object instance)
        {
            List<FieldInfo> fields = FieldsOf(instance.GetType());
            int saved = r.ReadInt(key + ".fields");
            if (saved != fields.Count)
            {
                throw new SaveFormatException(instance.GetType().Name + " had " + saved
                    + " fields when saved but has " + fields.Count + " now.");
            }
            for (int i = 0; i < fields.Count; i++)
            {
                FieldInfo field = fields[i];
                object current = field.GetValue(instance);
                object loaded = ReadValue(r, key + "." + field.Name, field.FieldType, current);
                // A collection is refilled in place (the field is usually readonly), so only a
                // replacement value needs writing back.
                if (!ReferenceEquals(loaded, current) || field.FieldType.IsValueType)
                {
                    field.SetValue(instance, loaded);
                }
            }
        }

        /// <summary>Instance fields, base class first, name-sorted inside each type.</summary>
        private static List<FieldInfo> FieldsOf(Type type)
        {
            var levels = new List<Type>();
            for (Type t = type; t != null && t != typeof(object); t = t.BaseType)
            {
                levels.Add(t);
            }
            levels.Reverse();
            var fields = new List<FieldInfo>();
            for (int i = 0; i < levels.Count; i++)
            {
                FieldInfo[] declared = levels[i].GetFields(DeclaredInstance);
                Array.Sort(declared,
                    delegate (FieldInfo a, FieldInfo b)
                    {
                        return string.CompareOrdinal(a.Name, b.Name);
                    });
                fields.AddRange(declared);
            }
            return fields;
        }

        // ------------------------------------------------------------------- writing

        private static void WriteValue(SaveWriter w, string key, Type type, object value)
        {
            Type nullable = Nullable.GetUnderlyingType(type);
            if (nullable != null)
            {
                w.Write(key + ".has", value != null);
                if (value != null)
                {
                    WriteValue(w, key + ".v", nullable, value);
                }
                return;
            }
            if (type.IsEnum)
            {
                w.Write(key, Convert.ToInt32(value));
                return;
            }
            if (type == typeof(int)) { w.Write(key, (int)value); return; }
            if (type == typeof(long)) { w.Write(key, (long)value); return; }
            if (type == typeof(bool)) { w.Write(key, (bool)value); return; }
            if (type == typeof(double)) { w.Write(key, (double)value); return; }
            if (type == typeof(float)) { w.Write(key, (double)(float)value); return; }
            if (type == typeof(string)) { w.Write(key, (string)value); return; }

            if (type == typeof(BlockShape))
            {
                // The streak jokers remember the last shape played. It is immutable and fully
                // described by its cells, so it is rebuilt rather than referenced.
                var shape = (BlockShape)value;
                w.Write(key + ".has", shape != null);
                if (shape != null)
                {
                    CoreSerializers.WriteShape(w, key + ".shape", shape);
                }
                return;
            }

            if (typeof(Power).IsAssignableFrom(type))
            {
                // "Halüsinasyon" wears another power. Its identity is its DefId; its own state
                // is written nested, so a morphed power keeps whatever it was holding.
                var power = (Power)value;
                w.Write(key + ".def", power != null ? power.DefId : null);
                if (power != null)
                {
                    Save(w, key + ".state", power);
                }
                return;
            }

            Type dictionaryValue;
            Type dictionaryKey;
            if (TryDictionaryTypes(type, out dictionaryKey, out dictionaryValue))
            {
                var map = (IDictionary)value;
                w.Write(key + ".count", map != null ? map.Count : 0);
                if (map == null)
                {
                    return;
                }
                int index = 0;
                foreach (DictionaryEntry entry in map)
                {
                    WriteValue(w, key + ".k" + index, dictionaryKey, entry.Key);
                    WriteValue(w, key + ".v" + index, dictionaryValue, entry.Value);
                    index++;
                }
                return;
            }

            Type element;
            if (TryCollectionElement(type, out element))
            {
                var items = (IEnumerable)value;
                var buffer = new List<object>();
                if (items != null)
                {
                    foreach (object item in items)
                    {
                        buffer.Add(item);
                    }
                }
                w.Write(key + ".count", buffer.Count);
                for (int i = 0; i < buffer.Count; i++)
                {
                    WriteValue(w, key + "." + i, element, buffer[i]);
                }
                return;
            }

            if (type.IsValueType)
            {
                // Any other struct: walked field by field, which covers GridPos, CubeAttachment
                // and the small per-joker records without naming any of them here.
                List<FieldInfo> fields = FieldsOf(type);
                for (int i = 0; i < fields.Count; i++)
                {
                    WriteValue(w, key + "." + fields[i].Name, fields[i].FieldType,
                        fields[i].GetValue(value));
                }
                return;
            }

            throw new SaveFormatException("No save support for field type " + type.FullName
                + " (at '" + key + "'). Add it to ContentStateSerializer.");
        }

        // ------------------------------------------------------------------- reading

        private static object ReadValue(SaveReader r, string key, Type type, object current)
        {
            Type nullable = Nullable.GetUnderlyingType(type);
            if (nullable != null)
            {
                return r.ReadBool(key + ".has") ? ReadValue(r, key + ".v", nullable, null) : null;
            }
            if (type.IsEnum)
            {
                return Enum.ToObject(type, r.ReadInt(key));
            }
            if (type == typeof(int)) return r.ReadInt(key);
            if (type == typeof(long)) return r.ReadLong(key);
            if (type == typeof(bool)) return r.ReadBool(key);
            if (type == typeof(double)) return r.ReadDouble(key);
            if (type == typeof(float)) return (float)r.ReadDouble(key);
            if (type == typeof(string)) return r.ReadString(key);

            if (type == typeof(BlockShape))
            {
                return r.ReadBool(key + ".has")
                    ? CoreSerializers.ReadShape(r, key + ".shape")
                    : null;
            }

            if (typeof(Power).IsAssignableFrom(type))
            {
                string defId = r.ReadString(key + ".def");
                if (defId == null)
                {
                    return null;
                }
                Power power = PowerRegistry.Create(defId);
                if (power == null)
                {
                    throw new SaveFormatException("Unknown power '" + defId + "' at '" + key + "'.");
                }
                Load(r, key + ".state", power);
                return power;
            }

            Type dictionaryKey;
            Type dictionaryValue;
            if (TryDictionaryTypes(type, out dictionaryKey, out dictionaryValue))
            {
                var map = (IDictionary)(current ?? Activator.CreateInstance(type));
                map.Clear();
                int count = r.ReadInt(key + ".count");
                for (int i = 0; i < count; i++)
                {
                    object k = ReadValue(r, key + ".k" + i, dictionaryKey, null);
                    object v = ReadValue(r, key + ".v" + i, dictionaryValue, null);
                    map[k] = v;
                }
                return map;
            }

            Type element;
            if (TryCollectionElement(type, out element))
            {
                int count = r.ReadInt(key + ".count");
                if (type.IsArray)
                {
                    Array array = Array.CreateInstance(element, count);
                    for (int i = 0; i < count; i++)
                    {
                        array.SetValue(ReadValue(r, key + "." + i, element, null), i);
                    }
                    return array;
                }
                object collection = current ?? Activator.CreateInstance(type);
                // List<T> and HashSet<T> both have Clear() and Add(T), but share no common
                // non-generic interface that exposes Add - so both are driven by reflection.
                MethodInfo clear = type.GetMethod("Clear", Type.EmptyTypes);
                MethodInfo add = type.GetMethod("Add", new[] { element });
                if (clear == null || add == null)
                {
                    throw new SaveFormatException("Collection " + type.FullName
                        + " has no Clear/Add to restore into (at '" + key + "').");
                }
                clear.Invoke(collection, null);
                var one = new object[1];
                for (int i = 0; i < count; i++)
                {
                    one[0] = ReadValue(r, key + "." + i, element, null);
                    add.Invoke(collection, one);
                }
                return collection;
            }

            if (type.IsValueType)
            {
                object box = current ?? Activator.CreateInstance(type);
                List<FieldInfo> fields = FieldsOf(type);
                for (int i = 0; i < fields.Count; i++)
                {
                    fields[i].SetValue(box,
                        ReadValue(r, key + "." + fields[i].Name, fields[i].FieldType, null));
                }
                return box;
            }

            throw new SaveFormatException("No save support for field type " + type.FullName
                + " (at '" + key + "'). Add it to ContentStateSerializer.");
        }

        // ------------------------------------------------------------------- helpers

        private static bool TryDictionaryTypes(Type type, out Type keyType, out Type valueType)
        {
            keyType = null;
            valueType = null;
            if (!type.IsGenericType || type.GetGenericTypeDefinition() != typeof(Dictionary<,>))
            {
                return false;
            }
            Type[] args = type.GetGenericArguments();
            keyType = args[0];
            valueType = args[1];
            return true;
        }

        private static bool TryCollectionElement(Type type, out Type element)
        {
            if (type.IsArray)
            {
                element = type.GetElementType();
                return true;
            }
            element = null;
            if (!type.IsGenericType)
            {
                return false;
            }
            Type definition = type.GetGenericTypeDefinition();
            if (definition != typeof(List<>) && definition != typeof(HashSet<>))
            {
                return false;
            }
            element = type.GetGenericArguments()[0];
            return true;
        }
    }
}
