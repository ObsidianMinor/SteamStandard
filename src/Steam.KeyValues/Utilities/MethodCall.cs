﻿namespace Steam.KeyValues.Utilities
{
    internal delegate TResult MethodCall<T, TResult>(T target, params object[] args);
}
