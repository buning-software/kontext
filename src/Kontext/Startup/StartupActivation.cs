namespace Kontext.Startup;

internal static class StartupActivation
{
    public const string Flag = "--startup";

    public static bool HasFlag(IEnumerable<string> args) =>
        args.Any(arg => string.Equals(arg, Flag, StringComparison.OrdinalIgnoreCase));
}
