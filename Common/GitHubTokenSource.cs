using static ToolHelpers;

internal static class GitHubTokenSource
{
    public static string GetAccessToken()
    {
        var tokenFilePath = Path.Combine(GetCurrentScriptDirectory(), "..", ".githubtoken");
        var token = Environment.GetEnvironmentVariable("GH_TOKEN");
        if (string.IsNullOrEmpty(token))
        {
            if (File.Exists(tokenFilePath))
            {
                token = File.ReadAllText(tokenFilePath).Trim();
            }
        }

        if (string.IsNullOrEmpty(token))
        {
            throw new Exception($"GitHub access token is missing; please put it in '{tokenFilePath}', or in the GH_TOKEN environment variable.");
        }

        return token;
    }
}
