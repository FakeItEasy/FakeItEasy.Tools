namespace FakeItEasy.PrepareRelease;

using Octokit;
using static ReleaseHelpers;

internal sealed class GitHubHelper
{
    private readonly IGitHubClient gitHubClient;
    private readonly string repoOwner;
    private readonly string repoName;

    public GitHubHelper(IGitHubClient gitHubClient, string repoOwner, string repoName)
    {
        this.gitHubClient = gitHubClient;
        this.repoOwner = repoOwner;
        this.repoName = repoName;
    }

    public async Task<Milestone> GetExistingMilestone(string existingMilestoneTitle)
    {
        Console.WriteLine($"Fetching milestone '{existingMilestoneTitle}'...");
        var milestoneRequest = new MilestoneRequest { State = ItemStateFilter.Open };
        var existingMilestone = (await this.gitHubClient.Issue.Milestone.GetAllForRepository(this.repoOwner, this.repoName, milestoneRequest))
            .Single(milestone => milestone.Title == existingMilestoneTitle);
        Console.WriteLine($"Fetched milestone '{existingMilestone.Title}'");
        return existingMilestone;
    }

    public async Task<IReadOnlyCollection<Release>> GetAllReleases()
    {
        Console.WriteLine("Fetching all GitHub releases...");
        var allReleases = await this.gitHubClient.Repository.Release.GetAll(this.repoOwner, this.repoName);
        Console.WriteLine("Fetched all GitHub releases");
        return allReleases;
    }

    public async Task<IList<Issue>> GetIssuesInMilestone(Milestone milestone)
    {
        Console.WriteLine($"Fetching issues in milestone '{milestone.Title}'...'");
        var issueRequest = new RepositoryIssueRequest { Milestone = milestone.Number.ToString(), State = ItemStateFilter.All };
        var issues = (await this.gitHubClient.Issue.GetAllForRepository(this.repoOwner, this.repoName, issueRequest)).ToList();
        Console.WriteLine($"Fetched {issues.Count} issues in milestone '{milestone.Title}'");
        return issues;
    }

    public async Task RenameMilestone(Milestone existingMilestone, string version)
    {
        var milestoneUpdate = new MilestoneUpdate { Title = version };
        Console.WriteLine($"Renaming milestone '{existingMilestone.Title}' to '{milestoneUpdate.Title}'...");
        var updatedMilestone = await this.gitHubClient.Issue.Milestone.Update(this.repoOwner, this.repoName, existingMilestone.Number, milestoneUpdate);
        Console.WriteLine($"Renamed milestone '{existingMilestone.Title}' to '{updatedMilestone.Title}'");
    }

    public async Task<Milestone> CreateNextMilestone(string nextReleaseName)
    {
        var newMilestone = new NewMilestone(nextReleaseName);
        Console.WriteLine($"Creating new milestone '{newMilestone.Title}'...");
        var nextMilestone = await this.gitHubClient.Issue.Milestone.Create(this.repoOwner, this.repoName, newMilestone);
        Console.WriteLine($"Created new milestone '{nextMilestone.Title}'");
        return nextMilestone;
    }

    public async Task UpdateRelease(Release existingRelease, string version)
    {
        var releaseUpdate = new ReleaseUpdate { Name = version, TagName = version, Prerelease = IsPreRelease(version) };
        Console.WriteLine($"Renaming GitHub release '{existingRelease.Name}' to {releaseUpdate.Name}...");
        var updatedRelease = await this.gitHubClient.Repository.Release.Edit(this.repoOwner, this.repoName, existingRelease.Id, releaseUpdate);
        Console.WriteLine($"Renamed GitHub release '{existingRelease.Name}' to {updatedRelease.Name}");
    }

    public async Task CreateNextRelease(string nextReleaseName)
    {
        const string newReleaseBody = @"
### Changed

### New
* Issue Title (#12345)

### Fixed

### Additional Items

### With special thanks for contributions to this release from:
* Real Name - @githubhandle
";

        var newRelease = new NewRelease(nextReleaseName) { Draft = true, Name = nextReleaseName, Body = newReleaseBody.Trim() };
        Console.WriteLine($"Creating new GitHub release '{newRelease.Name}'...");
        var nextRelease = await this.gitHubClient.Repository.Release.Create(this.repoOwner, this.repoName, newRelease);
        Console.WriteLine($"Created new GitHub release '{nextRelease.Name}'");
    }

    public async Task UpdateIssue(Issue existingIssue, Milestone existingMilestone, string version)
    {
        var issueUpdate = new IssueUpdate { Title = $"Release {version}", Milestone = existingMilestone.Number };
        Console.WriteLine($"Renaming release issue '{existingIssue.Title}' to '{issueUpdate.Title}'...");
        var updatedIssue = await this.gitHubClient.Issue.Update(this.repoOwner, this.repoName, existingIssue.Number, issueUpdate);
        Console.WriteLine($"Renamed release issue '{existingIssue.Title}' to '{updatedIssue.Title}'");
    }

    public async Task CreateNextIssue(Issue existingIssue, Milestone nextMilestone, string nextReleaseName)
    {
        var newIssue = new NewIssue($"Release {nextReleaseName}")
        {
            Milestone = nextMilestone.Number,
            Body = existingIssue.Body.Replace("[x]", "[ ]", StringComparison.OrdinalIgnoreCase),
        };
        foreach (var label in existingIssue.Labels)
        {
            newIssue.Labels.Add(label.Name);
        }

        Console.WriteLine($"Creating new release issue '{newIssue.Title}'...");
        var nextIssue = await this.gitHubClient.Issue.Create(this.repoOwner, this.repoName, newIssue);
        Console.WriteLine($"Created new release issue #{nextIssue.Number}: '{newIssue.Title}'");
    }
}
