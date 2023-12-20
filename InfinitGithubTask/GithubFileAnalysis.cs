using System.Text;
using Octokit;

namespace InfinitGithubTask;

public class GithubFileAnalysis(IGitHubClient client)
{
    private const string RepoOwner = "lodash";
    private const string RepoName = "lodash";

    public async Task<Dictionary<char, int>> AnalyzeRepoLetterFrequency()
    {
        Console.WriteLine("Analyzing repository started!");

        // Fetch the tree
        var treeResponse = await GetRepoTree(client);

        // Check if rate limit allows fetching these files
        await CheckRateLimit(client, treeResponse);

        // Analyze letter frequency
        var letterFrequency = await FetchAndAnalyzeContent(treeResponse, client, RepoOwner, RepoName);

        // Sort and display results
        DisplayByCountDesc(letterFrequency);
        return letterFrequency;
    }

    private async Task<TreeResponse> GetRepoTree(IGitHubClient client)
    {
        // Fetch the latest commit SHA
        var latestCommitSha = await GetLatestCommitSha(client, RepoOwner, RepoName);
        Console.WriteLine("Fetching the repository tree...");
        var treeResponse = await client.Git.Tree.GetRecursive(RepoOwner, RepoName, latestCommitSha);
        Console.WriteLine($"Number of items in tree: {treeResponse.Tree.Count}");
        return treeResponse;
    }

    private static async Task<string> GetLatestCommitSha(IGitHubClient client, string repoOwner, string repoName)
    {
        Console.WriteLine("Fetching the latest commit SHA...");
        var masterReference = await client.Git.Reference.Get(repoOwner, repoName, "heads/main");
        var latestCommitSha = masterReference.Object.Sha;
        Console.WriteLine($"Latest Commit SHA: {latestCommitSha}");
        return latestCommitSha;
    }

    private static async Task CheckRateLimit(IGitHubClient client, TreeResponse treeResponse)
    {
        var rateLimit = await client.RateLimit.GetRateLimits();
        Console.WriteLine($"Rate Limit: {rateLimit.Rate.Limit}, Remaining: {rateLimit.Rate.Remaining}");

        if (rateLimit.Rate.Remaining < GetTargetFilesCount(treeResponse))
        {
            throw new Exception("Rate limit may not be sufficient to fetch all files.");
            // log or schedule the job for later
        }
    }

    private static int GetTargetFilesCount(TreeResponse treeResponse)
    {
        var targetFilesCount = treeResponse.Tree.Count(item => item.Path.EndsWith(".js") || item.Path.EndsWith(".ts"));
        if (targetFilesCount is 0)
            throw new Exception("Repository does not contain any .js or .ts files!");

        Console.WriteLine($"Number of .js and .ts files: {targetFilesCount}");
        return targetFilesCount;
    }


    private async Task<Dictionary<char, int>> FetchAndAnalyzeContent(TreeResponse treeResponse,
        IGitHubClient client, string repoOwner, string repoName)
    {
        Console.WriteLine("Analyzing letter frequency...");
        var letterFrequency = new Dictionary<char, int>();
        foreach (var item in treeResponse.Tree.Where(i =>
                     i.Type == TreeType.Blob && (i.Path.EndsWith(".js") || i.Path.EndsWith(".ts"))))
        {
            Console.WriteLine($"Processing file: {item.Path}");
            var contents = await client.Repository.Content.GetAllContents(repoOwner, repoName, item.Path);
            var fileContent = contents[0].Content;
            CountLetters(fileContent, letterFrequency);
        }

        return letterFrequency;
    }

    private static void DisplayByCountDesc(Dictionary<char, int> letterFrequency)
    {
        Console.WriteLine("Letter frequency analysis completed. Results:");
        foreach (var item in letterFrequency.OrderByDescending(kv => kv.Value))
        {
            Console.WriteLine($"{item.Key}: {item.Value}");
        }
    }

    private static void CountLetters(string content, Dictionary<char, int> frequencies)
    {
        foreach (var charToCount in from c in content where char.IsLetter(c) select char.ToLower(c))
        {
            if (frequencies.TryGetValue(charToCount, out var value))
                frequencies[charToCount] = ++value;
            else
                frequencies[charToCount] = 1;
        }
    }
}