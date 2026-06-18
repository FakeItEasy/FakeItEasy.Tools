#!/usr/bin/env dotnet

#:package Octokit@$(OctokitVersion)
#:include ./Common/GitHubTokenSource.cs
#:include ./Common/ReleaseHelpers.cs
#:include ./Common/ToolHelpers.cs
#:include ./PrepareRelease/GitHubHelper.cs

using FakeItEasy.PrepareRelease;
using Octokit;
using static ReleaseHelpers;

if (args.Length != 4 || (args[1] != "next" && args[1] != "fork"))
{
    Console.WriteLine("Illegal arguments. Must be one of the following:");
    Console.WriteLine("  <repo> next <new release> <existing release>");
    Console.WriteLine("  <repo> fork <new release> <existing release>");
    return;
}

const string repoOwner = "FakeItEasy";
var repoName = args[0];
var action = args[1];
var version = args[2];
var existingReleaseName = args[3];

var gitHubClient = GetAuthenticatedGitHubClient();
var gitHubHelper = new GitHubHelper(gitHubClient, repoOwner, repoName);

var existingMilestone = await gitHubHelper.GetExistingMilestone(existingReleaseName);
var issuesInExistingMilestone = await gitHubHelper.GetIssuesInMilestone(existingMilestone);
var existingReleaseIssue = GetExistingReleaseIssue(issuesInExistingMilestone, existingReleaseName);

if (action == "next")
{
    var nextReleaseName = existingReleaseName;

    var allReleases = await gitHubHelper.GetAllReleases();
    var existingRelease = allReleases.Single(release => release.Name == existingReleaseName && release.Draft);

    var releasesForExistingMilestone = GetReleasesForExistingMilestone(allReleases, existingRelease, version);

    var nonReleaseIssuesInMilestone = ExcludeReleaseIssues(issuesInExistingMilestone, releasesForExistingMilestone);

    var issueNumbersReferencedFromReleases = GetIssueNumbersReferencedFromReleases(releasesForExistingMilestone);

    if (!CrossReferenceIssues(nonReleaseIssuesInMilestone, issueNumbersReferencedFromReleases))
    {
        return;
    }

    Milestone nextMilestone;
    if (IsPreRelease(version))
    {
        nextMilestone = existingMilestone;
    }
    else
    {
        await gitHubHelper.RenameMilestone(existingMilestone, version);
        nextMilestone = await gitHubHelper.CreateNextMilestone(nextReleaseName);
    }

    await gitHubHelper.UpdateRelease(existingRelease, version);
    await gitHubHelper.CreateNextRelease(nextReleaseName);
    await gitHubHelper.UpdateIssue(existingReleaseIssue, existingMilestone, version);
    await gitHubHelper.CreateNextIssue(existingReleaseIssue, nextMilestone, nextReleaseName);
}
else
{
    var nextReleaseName = version;

    var nextMilestone = await gitHubHelper.CreateNextMilestone(nextReleaseName);
    await gitHubHelper.CreateNextRelease(nextReleaseName);
    await gitHubHelper.CreateNextIssue(existingReleaseIssue, nextMilestone, nextReleaseName);
}

static List<Release> GetReleasesForExistingMilestone(IReadOnlyCollection<Release> allReleases, Release existingRelease, string version)
{
    var releasesForExistingMilestone = new List<Release> { existingRelease };
    var versionRoot = IsPreRelease(version) ? version.Substring(0, version.IndexOf('-', StringComparison.Ordinal)) : version;
    releasesForExistingMilestone.AddRange(allReleases.Where(release => release.Name.StartsWith(versionRoot, StringComparison.OrdinalIgnoreCase)));
    return releasesForExistingMilestone;
}

static GitHubClient GetAuthenticatedGitHubClient()
{
    var token = GitHubTokenSource.GetAccessToken();
    var credentials = new Credentials(token);
    return new GitHubClient(new ProductHeaderValue("FakeItEasy-build-scripts")) { Credentials = credentials };
}

static Issue GetExistingReleaseIssue(IList<Issue> issues, string existingReleaseName)
{
    var issue = issues.Single(i => i.Title == $"Release {existingReleaseName}");
    Console.WriteLine($"Found release issue #{issue.Number}: '{issue.Title}'");
    return issue;
}

static IList<Issue> ExcludeReleaseIssues(IList<Issue> issues, IEnumerable<Release> releases)
{
    return issues.Where(issue => releases.All(release => $"Release {release.Name}" != issue.Title)).ToList();
}

static bool CrossReferenceIssues(ICollection<Issue> issuesInMilestone, ICollection<int> issueNumbersReferencedFromRelease)
{
    var issueNumbersInMilestone = issuesInMilestone.Select(i => i.Number);
    var issueNumbersInReleaseButNotMilestone = issueNumbersReferencedFromRelease.Except(issueNumbersInMilestone).ToList();
    var issuesInMilestoneButNotRelease = issuesInMilestone.Where(i => !issueNumbersReferencedFromRelease.Contains(i.Number)).ToList();

    if (issuesInMilestoneButNotRelease.Count == 0 && issueNumbersInReleaseButNotMilestone.Count == 0)
    {
        Console.WriteLine("The release refers to the same issues included in the milestone. Congratulations.");
        return true;
    }

    Console.WriteLine();

    if (issuesInMilestoneButNotRelease.Count > 0)
    {
        Console.WriteLine("The following issues are linked to the milestone but not referenced in the release:");
        foreach (var issue in issuesInMilestoneButNotRelease)
        {
            Console.WriteLine($"  #{issue.Number}: {issue.Title}");
        }

        Console.WriteLine();
    }

    if (issueNumbersInReleaseButNotMilestone.Count > 0)
    {
        Console.WriteLine("The following issues are referenced in the release but not linked to the milestone:");
        foreach (var issueNumber in issueNumbersInReleaseButNotMilestone)
        {
            Console.WriteLine($"  #{issueNumber}");
        }

        Console.WriteLine();
    }

    Console.WriteLine("Prepare release anyhow? (y/N)");
    var response = Console.ReadLine()?.Trim();
    if (string.Equals(response, "y", StringComparison.OrdinalIgnoreCase))
    {
        return true;
    }

    if (string.Equals(response, "n", StringComparison.OrdinalIgnoreCase))
    {
        return false;
    }

    Console.WriteLine($"Unknown response '{response}' received. Treating as 'n'.");
    return false;
}
