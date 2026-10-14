namespace Erable.Core;

/// <summary>Anchor type used to locate the Erable.Core assembly (reflection, architecture tests).</summary>
public static class CoreAssembly
{
    public static System.Reflection.Assembly Assembly => typeof(CoreAssembly).Assembly;
}
