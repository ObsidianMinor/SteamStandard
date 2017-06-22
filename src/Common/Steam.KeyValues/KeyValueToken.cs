﻿namespace Steam.KeyValues
{
    /// <summary>
    /// Specifies a KeyValue token value type
    /// </summary>
    public enum KeyValueToken
    {
        /// <summary>
        /// No value
        /// </summary>
        None = 0,
        /// <summary>
        /// A string
        /// </summary>
        String = 1,
        /// <summary>
        /// An Int32
        /// </summary>
        Int32 = 2,
        /// <summary>
        /// A float
        /// </summary>
        Float32 = 3,
        /// <summary>
        /// A signed pointer
        /// </summary>
        Pointer = 4,
        /// <summary>
        /// A wide string
        /// </summary>
        WideString = 5,
        /// <summary>
        /// A color
        /// </summary>
        Color = 6,
        /// <summary>
        /// An unsigned Int64
        /// </summary>
        UInt64 = 7,
        /// <summary>
        /// An end token
        /// </summary>
        End = 8,
        /// <summary>
        /// A property name
        /// </summary>
        PropertyName = 9,
        /// <summary>
        /// An Int64
        /// </summary>
        Int64 = 10,
    }
}
