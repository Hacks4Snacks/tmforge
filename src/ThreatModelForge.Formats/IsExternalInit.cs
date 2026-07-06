namespace System.Runtime.CompilerServices
{
    using System.ComponentModel;

    /// <summary>
    /// Polyfill that enables C# <c>init</c>-only property setters when targeting netstandard2.0,
    /// which does not ship this type. Required by the <c>tmforge-json</c> model DTOs.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    internal static class IsExternalInit
    {
    }
}
