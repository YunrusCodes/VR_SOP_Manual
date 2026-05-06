// Polyfill for System.Runtime.CompilerServices.IsExternalInit, required by C# 9 record
// init-only setters. Unity's reference assemblies don't ship this type, so we provide
// our own internal stub. Safe to include in any TFM that doesn't already define it.

namespace System.Runtime.CompilerServices
{
    internal static class IsExternalInit { }
}
