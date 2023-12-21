using Moq;
using Octokit;

namespace InfinitGithubTask.Tests;

[TestFixture]
public class GithubFileAnalysisTests
{
#pragma warning disable CS8618
    private Mock<IGitHubClient> mockGitHubClient;
    private Mock<ITreesClient> mockTreesClient;
    private Mock<IGitDatabaseClient> mockGitDatabaseClient;
    private Mock<IRateLimitClient> mockRateLimitClient;
    private Mock<IRepositoryContentsClient> mockRepositoryContentClient;
    private GithubFileAnalysis githubFileAnalysis;
#pragma warning restore CS8618

    [SetUp]
    public void SetUp()
    {
        mockGitHubClient = new Mock<IGitHubClient>();
        mockTreesClient = new Mock<ITreesClient>();
        mockGitDatabaseClient = new Mock<IGitDatabaseClient>();
        mockRateLimitClient = new Mock<IRateLimitClient>();
        mockRepositoryContentClient = new Mock<IRepositoryContentsClient>();

        mockGitHubClient.Setup(c => c.Git).Returns(mockGitDatabaseClient.Object);
        mockGitHubClient.Setup(c => c.Repository.Content).Returns(mockRepositoryContentClient.Object);
        mockGitDatabaseClient.Setup(g => g.Tree).Returns(mockTreesClient.Object);
        // reference
        var mockReference = new Mock<IReferencesClient>();
        var fakeReference = CreateFakeReference();
        mockReference.Setup(r => r.Get(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(fakeReference);
        mockGitDatabaseClient.Setup(g => g.Reference).Returns(mockReference.Object);

        //rate limit
        var rateLimit = new RateLimit(5000, 4999, DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        var resources =
            new ResourceRateLimit(rateLimit, rateLimit);
        var miscellaneousRateLimit = new MiscellaneousRateLimit(resources, rateLimit);
        mockRateLimitClient.Setup(client => client.GetRateLimits()).ReturnsAsync(miscellaneousRateLimit);
        mockGitHubClient.Setup(c => c.RateLimit).Returns(mockRateLimitClient.Object);

        //tree response
        var fakeTreeResponse = CreateFakeTreeResponse();
        mockTreesClient.Setup(t => t.GetRecursive("lodash", "lodash", It.IsAny<string>()))
            .ReturnsAsync(fakeTreeResponse);

        //file (test file content)
        var fakeFileResponse = CreateFakeFileResponse();
        mockRepositoryContentClient.Setup(b => b.GetAllContents("lodash", "lodash", It.IsAny<string>()))
            .ReturnsAsync(fakeFileResponse);


        githubFileAnalysis = new GithubFileAnalysis(mockGitHubClient.Object);
    }

    [Test]
    public async Task AnalyzeRepoLetterFrequency_CallsRequiredMethods_AndReturnsCorrectLetterFrequency()
    {
        var expectedLetterFrequency = new Dictionary<char, int> { { 'a', 3 }, { 'b', 1 } };
        var result = await githubFileAnalysis.AnalyzeRepoLetterFrequency();

        mockTreesClient.Verify(t => t.GetRecursive("lodash", "lodash", It.IsAny<string>()), Times.Once);
        mockRepositoryContentClient.Verify(b => b.GetAllContents("lodash", "lodash", It.IsAny<string>()), Times.AtLeastOnce());
        Assert.That(expectedLetterFrequency, Is.EqualTo(result));
    }

    private TreeResponse CreateFakeTreeResponse()
    {
        var treeItems = new List<TreeItem>
        {
            new("file1.js", "100644", TreeType.Blob, 120, "file1.js_sha", ""),
            // new("file2.ts", "100644", TreeType.Blob, 150, "file2.ts_sha", ""),
            new("README.md", "100644", TreeType.Blob, 50, "README.md_sha", ""),
        };

        var fakeTreeSha = "fake_tree_sha";
        var fakeTreeUrl = "http://fakeurl.com";
        var truncated = false;

        return new TreeResponse(fakeTreeSha, fakeTreeUrl, treeItems.AsReadOnly(), truncated);
    }

    private IReadOnlyList<RepositoryContent> CreateFakeFileResponse()
    {
        string fileContent = "aaab";
        byte[] contentBytes = System.Text.Encoding.UTF8.GetBytes(fileContent);
        string base64Content = System.Convert.ToBase64String(contentBytes);

        string nodeId = "fake_node_id";
        string sha = "fake_blob_sha";
        int size = contentBytes.Length;

        // Assuming some values for the parameters that are not provided in your snippet
        string name = "fake_name";
        string path = "fake/path";
        ContentType type = ContentType.File; // Use the appropriate ContentType
        string downloadUrl = "http://fakeurl.com/download";
        string url = "http://fakeurl.com";
        string gitUrl = "http://fakeurl.com/git";
        string htmlUrl = "http://fakeurl.com/html";
        string target = null; // Assuming it's not a symlink
        string submoduleGitUrl = null; // Assuming it's not a submodule

        var repositoryContent = new RepositoryContent(name, path, sha, size, type, downloadUrl, url, gitUrl, htmlUrl,
            EncodingType.Base64.ToString(), base64Content, target, submoduleGitUrl);

        var list = new List<RepositoryContent> { repositoryContent };
        return list.AsReadOnly();
    }


    private Reference CreateFakeReference()
    {
        string @ref = "refs/heads/main";
        string nodeId = "fake_node_id";
        string url = "http://fakeurl.com";
        var user = new User();
        var repository = new Repository();
        var type = TaggedType.Commit;

        var tagObject = new TagObject(nodeId, url, "tag label", @ref, "fake_sha", user, repository, type);
        return new Reference(@ref, nodeId, url, tagObject);
    }
}