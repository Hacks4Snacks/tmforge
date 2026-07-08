namespace System.Runtime.CompilerServices
{
    using System.ComponentModel;

    /// <summary>
    /// Enables C# <c>init</c>-only property accessors on <c>netstandard2.0</c>, which does not ship
    /// this compiler-support type in-box. Referenced only by the compiler; never used at runtime.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    internal static class IsExternalInit
    {
    }
}
