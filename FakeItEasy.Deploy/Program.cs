﻿using FakeItEasy.Deploy;
using FakeItEasy.Tools;
using Octokit;
using static FakeItEasy.Tools.ReleaseHelpers;
using static SimpleExec.Command;

if (args.Length != 1)
{
    Console.WriteLine("Illegal arguments. Usage:");
    Console.WriteLine("<program> <artifactsFolder>");
}

string artifactsFolder = args[0];

var releaseName = GetAppVeyorTagName();
if (string.IsNullOrEmpty(releaseName))
{
    Console.WriteLine("No Appveyor tag name supplied. Not deploying.");
    return;
}

var nugetServerUrl = GetNuGetServerUrl();
var nugetApiKey = GetNuGetApiKey();
var (repoOwner, repoName) = GetRepositoryName();
var gitHubClient = GetAuthenticatedGitHubClient();

Console.WriteLine($"Deploying {releaseName}");
Console.WriteLine($"Looking for GitHub release {releaseName}");

var releases = await gitHubClient.Repository.Release.GetAll(repoOwner, repoName);
var release = releases.FirstOrDefault(r => r.Name == releaseName)
              ?? throw new Exception($"Can't find release {releaseName}");

const string artifactsPattern = "*.nupkg";

var artifacts = Directory.GetFiles(artifactsFolder, artifactsPattern);
if (!artifacts.Any())
{
    throw new Exception("Can't find any artifacts to publish");
}

Console.WriteLine($"Uploading artifacts to GitHub release {releaseName}");
foreach (var file in artifacts)
{
    await UploadArtifactToGitHubReleaseAsync(gitHubClient, release, file);
}

Console.WriteLine($"Pushing nupkgs to {nugetServerUrl}");
foreach (var file in artifacts)
{
    await UploadPackageToNuGetAsync(file, nugetServerUrl, nugetApiKey);
}

var issueNumbersInCurrentRelease = GetIssueNumbersReferencedFromReleases(new[] { release });
var preReleases = GetPreReleasesContributingToThisRelease(release, releases);
var issueNumbersInPreReleases = GetIssueNumbersReferencedFromReleases(preReleases);
var newIssueNumbers = issueNumbersInCurrentRelease.Except(issueNumbersInPreReleases);

Console.WriteLine($"Adding 'released as part of' notes to {newIssueNumbers.Count()} issues");
var commentText = $"This change has been released as part of [{repoName} {releaseName}](https://github.com/{repoOwner}/{repoName}/releases/tag/{releaseName}).";
await Task.WhenAll(newIssueNumbers.Select(n => gitHubClient.Issue.Comment.Create(repoOwner, repoName, n, commentText)));

Console.WriteLine("Finished deploying");

static IEnumerable<Release> GetPreReleasesContributingToThisRelease(Release release, IReadOnlyList<Release> releases)
{
    if (release.Prerelease)
    {
        return Enumerable.Empty<Release>();
    }

    string baseName = BaseName(release);
    return releases.Where(r => r.Prerelease && BaseName(r) == baseName);

    string BaseName(Release release) => release.Name.Split('-')[0];
}

static async Task UploadArtifactToGitHubReleaseAsync(GitHubClient client, Release release, string path)
{
    var name = Path.GetFileName(path);
    Console.WriteLine($"Uploading {name}");
    using (var stream = File.OpenRead(path))
    {
        var upload = new ReleaseAssetUpload
        {
            FileName = name,
            ContentType = "application/octet-stream",
            RawData = stream,
            Timeout = TimeSpan.FromSeconds(100)
        };

        var asset = await client.Repository.Release.UploadAsset(release, upload);
        Console.WriteLine($"Uploaded {asset.Name}");
    }
}

static async Task UploadPackageToNuGetAsync(string path, string nugetServerUrl, string nugetApiKey)
{
    string name = Path.GetFileName(path);
    Console.WriteLine($"Pushing {name}");
    await RunAsync(ToolPaths.NuGet, $"push \"{path}\" -ApiKey {nugetApiKey} -Source {nugetServerUrl} -NonInteractive -ForceEnglishOutput", noEcho: true);
    Console.WriteLine($"Pushed {name}");
}

static (string repoOwner, string repoName) GetRepositoryName()
{
    var repoNameWithOwner = GetRequiredEnvironmentVariable("APPVEYOR_REPO_NAME");
    var parts = repoNameWithOwner.Split('/');
    return (parts[0], parts[1]);
}

static GitHubClient GetAuthenticatedGitHubClient()
{
    var token = GitHubTokenSource.GetAccessToken();
    var credentials = new Credentials(token);
    return new GitHubClient(new ProductHeaderValue("FakeItEasy-build-scripts")) { Credentials = credentials };
}

static string? GetAppVeyorTagName() => Environment.GetEnvironmentVariable("APPVEYOR_REPO_TAG_NAME");

static string GetNuGetServerUrl() => GetRequiredEnvironmentVariable("NUGET_SERVER_URL");

static string GetNuGetApiKey() => GetRequiredEnvironmentVariable("NUGET_API_KEY");

static string GetRequiredEnvironmentVariable(string key)
{
    var environmentValue = Environment.GetEnvironmentVariable(key);
    if (string.IsNullOrEmpty(environmentValue))
    {
        throw new Exception($"Required environment variable {key} is not set. Unable to continue.");
    }

    return environmentValue;
}
