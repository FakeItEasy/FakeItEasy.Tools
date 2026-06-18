using System.Runtime.CompilerServices;

internal static class ToolHelpers
{
    public static string GetCurrentScriptDirectory([CallerFilePath] string path = "") => Path.GetDirectoryName(path)
        ?? throw new Exception("Can't find current script directory.");
}
