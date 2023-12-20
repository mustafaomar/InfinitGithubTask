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
    private Mock<IBlobsClient> mockBlobsClient;
    private Mock<IRateLimitClient> mockRateLimitClient;
    private GithubFileAnalysis githubFileAnalysis;
#pragma warning restore CS8618

    [SetUp]
    public void SetUp()
    {
        mockGitHubClient = new Mock<IGitHubClient>();
        mockTreesClient = new Mock<ITreesClient>();
        mockGitDatabaseClient = new Mock<IGitDatabaseClient>();
        mockBlobsClient = new Mock<IBlobsClient>();
        mockRateLimitClient = new Mock<IRateLimitClient>();

        mockGitHubClient.Setup(c => c.Git).Returns(mockGitDatabaseClient.Object);
        mockGitDatabaseClient.Setup(g => g.Tree).Returns(mockTreesClient.Object);
        mockGitDatabaseClient.Setup(g => g.Blob).Returns(mockBlobsClient.Object);
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

        //Blob (test file content)
        var fakeBlobResponse = CreateFakeBlobResponse();
        mockBlobsClient.Setup(b => b.Get("lodash", "lodash", It.IsAny<string>()))
            .ReturnsAsync(fakeBlobResponse);

        githubFileAnalysis = new GithubFileAnalysis(mockGitHubClient.Object);
    }

    [Test]
    public async Task AnalyzeRepoLetterFrequency_CallsRequiredMethods_AndReturnsCorrectLetterFrequency()
    {
        var expectedLetterFrequency = new Dictionary<char, int> { { 'a', 3 }, { 'b', 1 } };
        var result = await githubFileAnalysis.AnalyzeRepoLetterFrequency();

        mockTreesClient.Verify(t => t.GetRecursive("lodash", "lodash", It.IsAny<string>()), Times.Once);
        mockBlobsClient.Verify(b => b.Get("lodash", "lodash", It.IsAny<string>()), Times.AtLeastOnce());
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

    private Blob CreateFakeBlobResponse()
    {
        string fileContent = "aaab"; 
        byte[] contentBytes = System.Text.Encoding.UTF8.GetBytes(fileContent);
        string base64Content = System.Convert.ToBase64String(contentBytes);

        string nodeId = "fake_node_id";
        string sha = "fake_blob_sha";
        int size = contentBytes.Length;

        return new Blob(nodeId, base64Content, EncodingType.Base64, sha, size);
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