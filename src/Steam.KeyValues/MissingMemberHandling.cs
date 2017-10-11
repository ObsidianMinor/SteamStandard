﻿namespace Steam.KeyValues
{
    /// <summary>
    /// Specifies missing member handling options for the <see cref="KeyValueSerializer"/>.
    /// </summary>
    public enum MissingMemberHandling
    {
        /// <summary>
        /// Ignore a missing member and do not attempt to deserialize it.
        /// </summary>
        Ignore = 0,

        /// <summary>
        /// Throw a <see cref="KeyValueSerializationException"/> when a missing member is encountered during deserialization.
        /// </summary>
        Error = 1
    }
}
