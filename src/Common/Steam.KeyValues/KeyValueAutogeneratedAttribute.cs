﻿using System;

namespace Steam.KeyValues
{
    /// <summary>
    /// Instructs the <see cref="KeyValueSerializer"/> to use this as a collection for autogenerated keys
    /// </summary>
    public class KeyValueAutogeneratedAttribute : Attribute
    {
        /// <summary>
        /// Gets or sets the item value converter for this collection
        /// </summary>
        public Type ItemValueConverter { get; set; }

        /// <summary>
        /// Gets or sets the <see cref="ItemValueConverter"/> parameters
        /// </summary>
        public object[] ItemConverterParameters { get; set; }
    }
}
