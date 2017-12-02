﻿using System;
using System.Buffers;
using System.Buffers.Text;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Text.Utf8;

using static System.Buffers.Binary.BinaryPrimitives;

namespace Steam.KeyValues
{
    /// <summary>
    /// Represents a KeyValue structure that is immutable, stack-only, and uses zero allocations
    /// </summary>
    [DebuggerDisplay("{DebuggerDisplay,nq}")]
    [DebuggerTypeProxy(typeof(TypeProxy))]
    public readonly ref struct ImmutableKeyValue
    {
        private readonly MemoryPool<byte> _pool;
        private readonly OwnedMemory<byte> _dbMemory;
        private readonly ReadOnlySpan<byte> _db;
        private readonly ReadOnlySpan<byte> _values;
        private readonly bool _binarySpan;
        
        internal ImmutableKeyValue(ReadOnlySpan<byte> values, ReadOnlySpan<byte> db, bool binary, MemoryPool<byte> pool = null, OwnedMemory<byte> dbMemory = null)
        {
            _values = values;
            _db = db;
            _pool = pool;
            _dbMemory = dbMemory;
            _binarySpan = binary;
        }
        
        internal DbRow Record => ReadMachineEndian<DbRow>(_db);

        /// <summary>
        /// Get key of this <see cref="ImmutableKeyValue"/> as a <see cref="string"/>
        /// </summary>
        public string Key => Utf8Key.ToString();

        /// <summary>
        /// Gets the key of this <see cref="ImmutableKeyValue"/> as a <see cref="Utf8Span"/>
        /// </summary>
        [CLSCompliant(false)]
        public Utf8Span Utf8Key
        {
            get
            {
                var record = Record;
                return new Utf8Span(_values.Slice(record.KeyLocation, record.KeyLength));
            }
        }

        /// <summary>
        /// Gets the type of value this <see cref="ImmutableKeyValue"/> contains
        /// </summary>
        public KeyValueType Type => ReadMachineEndian<KeyValueType>(_db.Slice(16));

        /// <summary>
        /// Parses the specified byte array as a text stream
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        public static ImmutableKeyValue Parse(byte[] data) => Parse(new ReadOnlySpan<byte>(data));
        
        /// <summary>
        /// Parses the specified <see cref="ReadOnlySpan{T}"/> as a text stream
        /// </summary>
        /// <param name="utf8KeyValue"></param>
        /// <param name="pool"></param>
        /// <returns></returns>
        [CLSCompliant(false)]
        public static ImmutableKeyValue Parse(ReadOnlySpan<byte> utf8KeyValue, MemoryPool<byte> pool = null)
        {
            return new KeyValueTextParser().Parse(utf8KeyValue, pool);
        }
        
        /// <summary>
        /// Parses the specified byte array as a binary stream
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        public static ImmutableKeyValue ParseBinary(byte[] data) => Parse(new ReadOnlySpan<byte>(data));

        /// <summary>
        /// Parses the specified <see cref="ReadOnlySpan{T}"/> as a binary stream
        /// </summary>
        /// <param name="binaryKeyValue"></param>
        /// <param name="pool"></param>
        /// <returns></returns>
        [CLSCompliant(false)]
        public static ImmutableKeyValue ParseBinary(ReadOnlySpan<byte> binaryKeyValue, MemoryPool<byte> pool = null)
        {
            return new KeyValueBinaryParser().Parse(binaryKeyValue, pool);
        }
        
        /// <summary>
        /// Gets the child <see cref="ImmutableKeyValue"/> with the specified key
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        [CLSCompliant(false)]
        public ImmutableKeyValue this[Utf8Span name] => TryGetValue(name, out var value) ? value : throw new KeyNotFoundException();
        
        /// <summary>
        /// Gets the child <see cref="ImmutableKeyValue"/> with the specified key
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public ImmutableKeyValue this[string name] => TryGetValue(name, out var value) ? value : throw new KeyNotFoundException();

        /// <summary>
        /// Gets the child <see cref="ImmutableKeyValue"/> at the specified index
        /// </summary>
        /// <param name="index">The index to get the child</param>
        /// <returns>The <see cref="ImmutableKeyValue"/> at the specified index</returns>
        public ImmutableKeyValue this[int index]
        {
            get
            {
                if (index < 0 || index >= Length)
                    throw new ArgumentOutOfRangeException(nameof(index));

                var record = Record;
                
                for (int i = DbRow.Size, pos = 0; i < _db.Length; i += DbRow.Size, pos++)
                {
                    record = ReadMachineEndian<DbRow>(_db.Slice(i));

                    if (pos == index)
                    {
                        int newStart = i;
                        int newEnd = i + DbRow.Size;

                        if (!record.IsSimpleValue)
                        {
                            newEnd += DbRow.Size * record.Length;
                        }
                        return new ImmutableKeyValue(_values, _db.Slice(newStart, newEnd - newStart), _binarySpan);
                    }

                    if (!record.IsSimpleValue)
                    {
                        i += record.Length * DbRow.Size;
                    }
                }

                throw new InvalidOperationException("This code is thought to be unreachable");
            }
        }

        /// <summary>
        /// Gets the number of children in this <see cref="ImmutableKeyValue"/>
        /// </summary>
        public int Length
        {
            get
            {
                var record = Record;
                if (!record.IsSimpleValue)
                {
                    int length = record.Length;

                    for (int i = DbRow.Size; i < _db.Length; i += DbRow.Size) // go through each row and subtract the members member count
                    {
                        record = ReadMachineEndian<DbRow>(_db.Slice(i));

                        if (!record.IsSimpleValue)
                        {
                            length -= record.Length;
                            i += record.Length * DbRow.Size;
                            continue;
                        }
                    }

                    return length;
                }
                else
                    return 0;
            }
        }

        /// <summary>
        /// Converts this <see cref="ImmutableKeyValue"/> to a mutable KeyValue object
        /// </summary>
        /// <returns></returns>
        public KeyValue ToKeyValue()
        {
            throw new NotImplementedException();
        }
        
        /// <summary>
        /// Writes this <see cref="ImmutableKeyValue"/> to the specified <see cref="Stream"/> in text format
        /// </summary>
        /// <param name="stream">The stream to write to</param>
        public void WriteTo(Stream stream)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Writes this <see cref="ImmutableKeyValue"/> to the specified <see cref="Stream"/> in binary format
        /// </summary>
        /// <param name="stream">The stream to write to</param>
        public void WriteBinaryTo(Stream stream)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Tries to get the <see cref="ImmutableKeyValue"/> with the specified key
        /// </summary>
        /// <param name="propertyName"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        [CLSCompliant(false)]
        public bool TryGetValue(Utf8Span propertyName, out ImmutableKeyValue value)
        {
            var record = Record;
            
            if (record.Type != KeyValueType.None)
                throw new InvalidOperationException();

            foreach(ImmutableKeyValue keyValue in this)
            {
                if (keyValue.Utf8Key == propertyName)
                {
                    value = keyValue;
                    return true;
                }
            }

            value = default;
            return false;
        }

        /// <summary>
        /// Tries to get the <see cref="ImmutableKeyValue"/> with the specified key
        /// </summary>
        /// <param name="propertyName"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public bool TryGetValue(string propertyName, out ImmutableKeyValue value)
        {
            var record = Record;

            if (record.Length == 0)
                throw new KeyNotFoundException();

            if (record.Type != KeyValueType.None)
                throw new InvalidOperationException();

            foreach (ImmutableKeyValue keyValue in this)
            {
                if (keyValue.Utf8Key == propertyName)
                {
                    value = keyValue;
                    return true;
                }
            }

            value = default;
            return false;
        }

        /// <summary>
        /// Gets the value of this <see cref="ImmutableKeyValue"/> as a 32 bit integer
        /// </summary>
        /// <returns>The value of this <see cref="ImmutableKeyValue"/></returns>
        public int GetInt()
        {
            if (TryGetInt(out var value))
                return value;
            else
                throw new InvalidCastException();
        }
        
        public bool TryGetInt(out int value)
        {
            value = default;

            if (Type != KeyValueType.Int32)
                return false;

            var record = Record;
            var span = _db.Slice(record.Location, record.Length);

            if (_binarySpan)
            {
                value = ReadInt32BigEndian(span);
                return true;
            }
            else
            {
                return Utf8Parser.TryParse(span, out value, out var _);
            }
        }

        [CLSCompliant(false)]
        public ulong GetUInt64()
        {
            if (TryGetUInt64(out var value))
                return value;
            else
                throw new InvalidCastException();
        }

        [CLSCompliant(false)]
        public bool TryGetUInt64(out ulong value) => throw new NotImplementedException();
        
        public long GetInt64()
        {
            if (TryGetInt64(out var value))
                return value;
            else
                throw new InvalidCastException();
        }

        public bool TryGetInt64(out long value) => throw new NotImplementedException();

        /// <summary>
        /// Gets the value of this <see cref="ImmutableKeyValue"/> as a UInt64
        /// </summary>
        /// <returns>A decimal container for an UInt64</returns>
        public decimal GetDecimal() => GetUInt64();

        public bool TryGetDecimal(out decimal value)
        {
            bool success = TryGetUInt64(out var ulongValue);
            value = ulongValue;
            return success;
        }

        public IntPtr GetPtr()
        {
            if (TryGetPtr(out var value))
                return value;
            else
                throw new InvalidCastException();
        }

        public bool TryGetPtr(out IntPtr value) => throw new NotImplementedException();

        public float GetFloat()
        {
            if (TryGetFloat(out var value))
                return value;
            else
                throw new InvalidCastException();
        }

        public bool TryGetFloat(out float value) => throw new NotImplementedException();

        [CLSCompliant(false)]
        public Utf8String GetUtf8String()
        {
            if (TryGetUtf8String(out var value))
                return value;
            else
                throw new InvalidCastException();
        }

        [CLSCompliant(false)]
        public bool TryGetUtf8String(out Utf8String value)
        {
            value = default;

            var record = Record;
            if (!record.IsSimpleValue)
                return false;

            value = new Utf8String(_values.Slice(record.Location, record.Length));
            return true;
        }

        public string GetString()
        {
            if (TryGetString(out var value))
                return value;
            else
                throw new InvalidCastException();
        }

        public bool TryGetString(out string value)
        {
            value = default;

            if (!TryGetUtf8String(out var utf8))
                return false;
            else
            {
                value = utf8.ToString();
                return true;
            }
        }

        public bool GetBool()
        {
            if (TryGetBool(out var value))
                return value;
            else
                throw new InvalidCastException();
        }

        public bool TryGetBool(out bool value)
        {
            value = default;

            if (!TryGetInt(out int val))
                return false;
            else
            {
                value = val != 0;
                return true;
            }
        }

        public Color GetColor()
        {
            if (TryGetColor(out var value))
                return value;
            else
                throw new InvalidCastException();
        }

        public bool TryGetColor(out Color value)
        {
            value = default;

            var record = Record;
            if (!record.IsSimpleValue)
                throw new InvalidCastException();

            var slice = _values.Slice(record.Location);

            switch(Type)
            {
                case KeyValueType.Color:
                case KeyValueType.Int32: // um
                case KeyValueType.Float: // ok valve...
                    if (slice.Length < 4)
                        return false;

                    byte r = slice[0];
                    byte g = slice[1];
                    byte b = slice[2];
                    byte a = slice[3];

                    value = Color.FromArgb(a, r, g, b);
                    return true;
                case KeyValueType.String:
                    // todo: implement color parsing
                    throw new NotImplementedException();
            }

            return false;
        }

        private string DebuggerDisplay => $"Key = \"{Key}\", {(Type == 0 ? $"Length = {Length}" : $"Value = \"{GetString()}\" ({Type})")}";

        public static explicit operator Color(ImmutableKeyValue kv) => kv.GetColor();
        public static explicit operator bool(ImmutableKeyValue kv) => kv.GetBool();
        [CLSCompliant(false)]
        public static explicit operator Utf8String(ImmutableKeyValue kv) => kv.GetUtf8String();
        public static explicit operator string(ImmutableKeyValue kv) => kv.GetString();
        public static explicit operator float(ImmutableKeyValue kv) => kv.GetFloat();
        public static explicit operator long(ImmutableKeyValue kv) => kv.GetInt64();
        [CLSCompliant(false)]
        public static explicit operator ulong(ImmutableKeyValue kv) => kv.GetUInt64();
        public static explicit operator int(ImmutableKeyValue kv) => kv.GetInt();
        public static explicit operator IntPtr(ImmutableKeyValue kv) => kv.GetPtr();
        public static explicit operator decimal(ImmutableKeyValue kv) => kv.GetDecimal();

        public Enumerator GetEnumerator() => new Enumerator(this);

        public void Dispose()
        {
            if (_pool == null)
                throw new InvalidOperationException("Only the root object can be disposed");

            _dbMemory.Dispose();
        }
        
        /// <summary>
        /// Provides an enumerator for enumerating through a KeyValue's subkeys
        /// </summary>
        public ref struct Enumerator // todo: optimize this
        {
            private readonly ImmutableKeyValue _keyValue;
            private int _currentIndex;
            private int _length;

            internal Enumerator(ImmutableKeyValue keyValue)
            {
                _keyValue = keyValue;
                _currentIndex = -1;
                _length = _keyValue.Length;
            }
            
            public ImmutableKeyValue Current => _keyValue[_currentIndex];

            public bool MoveNext()
            {
                _currentIndex++;
                return _currentIndex < _length;
            }
        }
        
        public class TypeProxy
        {
            public const string TestStringProxy = "Test";

            public TypeProxy(ImmutableKeyValue value)
            {
                List<object> list = new List<object>();
                foreach (ImmutableKeyValue child in value)
                    list.Add(child.Type == 0 ? (object)new ValuesTypeProxy(child) : new ValueTypeProxy(child));

                Items = list.ToArray();
            }
            
            [DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
            public object[] Items { get; }

            [DebuggerDisplay("{_value} ({_type})", Name = "{_key}")]
            private class ValueTypeProxy
            {
                private string _key;
                private string _value;
                private KeyValueType _type;

                public ValueTypeProxy(ImmutableKeyValue kv)
                {
                    _key = kv.Key;
                    _value = kv.GetString();
                    _type = kv.Type;
                }
            }

            [DebuggerDisplay("Length = {Items.Length}", Name = "{_key}")]
            private class ValuesTypeProxy
            {
                private string _key;

                public ValuesTypeProxy(ImmutableKeyValue kv)
                {
                    _key = kv.Key;

                    List<object> list = new List<object>();
                    foreach (ImmutableKeyValue child in kv)
                        list.Add(child.Type == 0 ? (object)new ValuesTypeProxy(child) : new ValueTypeProxy(child));

                    Items = list.ToArray();
                }

                [DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
                public object[] Items { get; }
            }
        }
    }
}
