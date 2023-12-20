using Octokit;

namespace InfinitGithubTask;

class Program
{
    static async Task Main(string[] args)
    {
        try
        {
            var client = CreateGitHubClient();
            
            // Create an instance of GithubFileAnalysis
            var githubFileAnalysis = new GithubFileAnalysis(client);

            // Call AnalyzeRepoLetterFrequency
            await githubFileAnalysis.AnalyzeRepoLetterFrequency();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }
    }

    private static GitHubClient CreateGitHubClient()
    {
        var tokenAuth = new Credentials("YOUR_TOKEN");
        var client = new GitHubClient(new ProductHeaderValue("InfinitGithubTask"))
        {
            Credentials = tokenAuth
        };
        return client;
    }
}