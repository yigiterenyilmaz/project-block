// PURPOSE: The save file format - a writer, a reader, and the error they throw. Pure C#
// like the rest of Core, so it works in the test harness as well as in Unity (no JsonUtility).
//
// THE FORMAT is one "key=value" per line, written and read in the SAME ORDER. The reader is
// POSITIONAL: it walks the entries in order and asserts that each key is the one it expected.
// That is deliberate, and it is the whole version-safety story:
//
//   a save written by an older build has different keys in different places, so the very first
//   drifted field throws SaveFormatException instead of quietly loading half a run.
//
// Loud failure beats silent corruption here - a half-restored run would look playable and be
// subtly wrong, which is far worse than "that save is from an older version".
//
// EVERY number is written and parsed with InvariantCulture. This is not optional: the team
// runs Turkish locales, where a plain double.ToString() writes "0,1" and the reader would
// then either throw or read a different number.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace ProjectBlock.Core
{
    /// <summary>Thrown when a save file is not what the reader expected - a truncated file, a
    /// corrupted line, or (the common case) a save from a build whose fields have moved.</summary>
    public sealed class SaveFormatException : Exception
    {
        public SaveFormatException(string message)
            : base(message)
        {
        }
    }

    /// <summary>Writes a save file. Call the Write overloads in a fixed order; SaveReader
    /// must then read them back in exactly that order.</summary>
    public sealed class SaveWriter
    {
        private readonly StringBuilder text = new StringBuilder();

        /// <summary>Marker for a null string. Escaping runs first and turns a real backslash
        /// into two, so no genuine string can ever collide with this.</summary>
        internal const string NullMarker = "\\0";

        public void Write(string key, int value)
        {
            Append(key, value.ToString(CultureInfo.InvariantCulture));
        }

        public void Write(string key, long value)
        {
            Append(key, value.ToString(CultureInfo.InvariantCulture));
        }

        public void Write(string key, bool value)
        {
            Append(key, value ? "1" : "0");
        }

        public void Write(string key, double value)
        {
            // "R" round-trips exactly, so a reloaded run scores identically.
            Append(key, value.ToString("R", CultureInfo.InvariantCulture));
        }

        public void Write(string key, string value)
        {
            Append(key, value == null ? NullMarker : Escape(value));
        }

        /// <summary>The whole file.</summary>
        public string ToText()
        {
            return text.ToString();
        }

        private void Append(string key, string value)
        {
            text.Append(key).Append('=').Append(value).Append('\n');
        }

        internal static string Escape(string value)
        {
            // Backslash FIRST, or the escapes below would be escaped again on the way out.
            return value.Replace("\\", "\\\\").Replace("\n", "\\n").Replace("\r", "\\r");
        }

        internal static string Unescape(string value)
        {
            var sb = new StringBuilder(value.Length);
            for (int i = 0; i < value.Length; i++)
            {
                if (value[i] != '\\' || i + 1 >= value.Length)
                {
                    sb.Append(value[i]);
                    continue;
                }
                i++;
                switch (value[i])
                {
                    case 'n': sb.Append('\n'); break;
                    case 'r': sb.Append('\r'); break;
                    default: sb.Append(value[i]); break; // covers the doubled backslash
                }
            }
            return sb.ToString();
        }
    }

    /// <summary>Reads a save file written by SaveWriter, positionally (see the file header).</summary>
    public sealed class SaveReader
    {
        private readonly List<string> keys = new List<string>();
        private readonly List<string> values = new List<string>();
        private int index;

        public SaveReader(string fileText)
        {
            if (fileText == null)
            {
                throw new SaveFormatException("The save file is empty.");
            }
            string[] lines = fileText.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].TrimEnd('\r');
                if (line.Length == 0)
                {
                    continue;
                }
                int split = line.IndexOf('=');
                if (split < 0)
                {
                    throw new SaveFormatException("Line " + (i + 1) + " has no '=': " + line);
                }
                keys.Add(line.Substring(0, split));
                values.Add(line.Substring(split + 1));
            }
        }

        /// <summary>True once every entry has been read - a whole-file sanity check.</summary>
        public bool AtEnd
        {
            get { return index >= keys.Count; }
        }

        public int ReadInt(string key)
        {
            return int.Parse(Take(key), CultureInfo.InvariantCulture);
        }

        public long ReadLong(string key)
        {
            return long.Parse(Take(key), CultureInfo.InvariantCulture);
        }

        public bool ReadBool(string key)
        {
            return Take(key) == "1";
        }

        public double ReadDouble(string key)
        {
            return double.Parse(Take(key), CultureInfo.InvariantCulture);
        }

        public string ReadString(string key)
        {
            string raw = Take(key);
            return raw == SaveWriter.NullMarker ? null : SaveWriter.Unescape(raw);
        }

        /// <summary>Takes the next value, asserting it is the expected key. The assert is what
        /// turns "this save is from an older build" into a clean error at the first drifted
        /// field, rather than a run that loads wrong.</summary>
        private string Take(string key)
        {
            if (index >= keys.Count)
            {
                throw new SaveFormatException("The save file ends before '" + key + "'.");
            }
            if (keys[index] != key)
            {
                throw new SaveFormatException("Expected '" + key + "' but found '"
                    + keys[index] + "' at entry " + index + ".");
            }
            return values[index++];
        }
    }
}
