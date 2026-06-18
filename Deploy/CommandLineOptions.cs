namespace FakeItEasy.Deploy;

internal sealed record CommandLineOptions(string Repo, string TagName, string ArtifactsFolder, bool DryRun, bool ShowHelp)
{
    public static CommandLineOptions Parse(string[] args)
    {
        string repo = string.Empty;
        string tagName = string.Empty;
        string artifactsFolder = string.Empty;
        bool dryRun = false;
        bool showHelp = false;

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "-h":
                case "--help":
                    showHelp = true;
                    break;
                case "-r":
                case "--repo":
                    repo = ReadNext();
                    break;
                case "-t":
                case "--tag-name":
                    tagName = ReadNext();
                    break;
                case "-a":
                case "--artifacts-folder":
                    artifactsFolder = ReadNext();
                    break;
                case "-d":
                case "--dry-run":
                    dryRun = true;
                    break;
                default:
                    ShowUsage();
                    throw new Exception($"Unknown command line option '{args[i]}'");
            }

            string ReadNext()
            {
                if (i + 1 >= args.Length)
                {
                    ShowUsage();
                    throw new Exception($"Expected value for option '{args[i]}', but none was provided");
                }

                return args[++i];
            }
        }

        return new CommandLineOptions(repo, tagName, artifactsFolder, dryRun, showHelp);
    }

    public static void ShowUsage()
    {
        Console.WriteLine("Usage: <program> <options>");
        Console.WriteLine("  -h|--help              Shows this help message");
        Console.WriteLine("  -r|--repo              [REQUIRED] Repository (as \"<owner>/<repo>\")");
        Console.WriteLine("  -t|--tag-name          [REQUIRED] Tag name");
        Console.WriteLine("  -a|--artifacts-folder  [REQUIRED] Artifacts folder");
        Console.WriteLine("  -d|--dry-run           Dry run (don't publish anything)");
        Console.WriteLine();
    }

    public void Validate()
    {
        var errors = new List<string>();
        if (string.IsNullOrEmpty(this.Repo))
        {
            errors.Add("Repository must be specified");
        }

        if (string.IsNullOrEmpty(this.TagName))
        {
            errors.Add("Tag name must be specified");
        }

        if (string.IsNullOrEmpty(this.ArtifactsFolder))
        {
            errors.Add("Artifacts folder must be specified");
        }

        if (errors.Count > 0)
        {
            foreach (var error in errors)
            {
                Console.Error.WriteLine(error);
            }

            ShowUsage();
            throw new ArgumentException("Invalid arguments");
        }
    }
}
