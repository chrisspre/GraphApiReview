using Microsoft.TeamFoundation.SourceControl.WebApi;
using Microsoft.VisualStudio.Services.WebApi;
using gapir.Services;
using gapir.Models;
using Xunit;

namespace gapir.Tests.Services;

/// <summary>
/// Unit tests for PullRequestAnalysisService focusing on reviewer filtering logic
/// </summary>
public class PullRequestAnalysisServiceTests
{
    private readonly Guid _currentUserId = Guid.NewGuid();
    private readonly string _currentUserDisplayName = "Test User";
    private readonly HashSet<string> _apiReviewersMembers;
    private readonly PullRequestAnalysisService _service;

    public PullRequestAnalysisServiceTests()
    {
        // Setup API reviewers group with test users
        _apiReviewersMembers = new HashSet<string>
        {
            "api-reviewer1@microsoft.com",
            "api-reviewer2@microsoft.com", 
            "api-reviewer3@microsoft.com",  // Adding third API reviewer
            _currentUserId.ToString() // Current user is an API reviewer
        };

        _service = new PullRequestAnalysisService(
            _apiReviewersMembers, 
            _currentUserId, 
            _currentUserDisplayName);
    }

    [Fact]
    public void GetApiApprovalRatio_WithRequiredReviewers_CountsOnlyRequired()
    {
        // Arrange
        var pr = CreatePullRequestWithReviewers(
            ("api-reviewer1@microsoft.com", vote: 10, isRequired: true),   // Approved, required
            ("api-reviewer2@microsoft.com", vote: 0, isRequired: true),    // No vote, required  
            ("api-reviewer3@microsoft.com", vote: 10, isRequired: false)   // Approved but not required
        );

        // Act
        var ratio = GetApiApprovalRatioFromService(pr);

        // Assert
        Assert.Equal("1/2", ratio); // Only required reviewers counted: 1 approved out of 2 total
    }

    [Fact]
    public void GetApiApprovalRatio_WithNoRequiredApiReviewers_ReturnsZeroSlashZero()
    {
        // Arrange
        var pr = CreatePullRequestWithReviewers(
            ("api-reviewer1@microsoft.com", vote: 10, isRequired: false),  // Not required
            ("non-api-reviewer@microsoft.com", vote: 10, isRequired: true) // Required but not API reviewer
        );

        // Act
        var ratio = GetApiApprovalRatioFromService(pr);

        // Assert
        Assert.Equal("0/0", ratio); // No required API reviewers
    }

    [Fact]
    public void GetMyVoteStatus_WhenCurrentUserIsRequiredReviewer_ReturnsVoteStatus()
    {
        // Arrange
        var pr = CreatePullRequestWithReviewers(
            (_currentUserId.ToString(), vote: 5, isRequired: true) // Current user approved with suggestions
        );

        // Act
        var status = GetMyVoteStatusFromService(pr);

        // Assert
        Assert.Equal("Sugges", status); // Approved with suggestions
    }

    [Fact]
    public void GetMyVoteStatus_WhenCurrentUserIsNotRequired_ReturnsNotReviewer()
    {
        // Arrange
        var pr = CreatePullRequestWithReviewers(
            (_currentUserId.ToString(), vote: 10, isRequired: false) // Current user approved but not required
        );

        // Act
        var status = GetMyVoteStatusFromService(pr);

        // Assert
        Assert.Equal("---", status); // Not a required reviewer
    }

    [Fact]
    public void GetMyVoteStatus_WhenCurrentUserNotInReviewersList_ReturnsNotReviewer()
    {
        // Arrange
        var pr = CreatePullRequestWithReviewers(
            ("other-user@microsoft.com", vote: 10, isRequired: true) // Other user, not current user
        );

        // Act
        var status = GetMyVoteStatusFromService(pr);

        // Assert
        Assert.Equal("---", status); // Not a reviewer
    }

    [Fact]
    public void IsApprovedByCurrentUser_WhenRequiredAndApproved_ReturnsTrue()
    {
        // Arrange
        var pr = CreatePullRequestWithReviewers(
            (_currentUserId.ToString(), vote: 10, isRequired: true) // Current user approved and required
        );

        // Act
        var isApproved = GetIsApprovedByCurrentUserFromService(pr);

        // Assert
        Assert.True(isApproved);
    }

    [Fact]
    public void IsApprovedByCurrentUser_WhenApprovedButNotRequired_ReturnsFalse()
    {
        // Arrange
        var pr = CreatePullRequestWithReviewers(
            (_currentUserId.ToString(), vote: 10, isRequired: false) // Current user approved but not required
        );

        // Act
        var isApproved = GetIsApprovedByCurrentUserFromService(pr);

        // Assert
        Assert.False(isApproved); // Not approved because not required
    }

    [Theory]
    [InlineData(10, "Apprvd")]   // Approved
    [InlineData(5, "Sugges")]    // Approved with suggestions
    [InlineData(0, "NoVote")]    // No vote
    [InlineData(-5, "Wait4A")]   // Waiting for author
    [InlineData(-10, "Reject")]  // Rejected
    [InlineData(99, "Unknow")]   // Unknown vote value
    public void GetMyVoteStatus_WithDifferentVoteValues_ReturnsCorrectStatus(int vote, string expectedStatus)
    {
        // Arrange
        var pr = CreatePullRequestWithReviewers(
            (_currentUserId.ToString(), vote: vote, isRequired: true)
        );

        // Act
        var status = GetMyVoteStatusFromService(pr);

        // Assert
        Assert.Equal(expectedStatus, status);
    }

    [Fact]
    public void GetApiApprovalRatio_WithApprovedWithSuggestions_CountsAsApproved()
    {
        // Arrange
        var pr = CreatePullRequestWithReviewers(
            ("api-reviewer1@microsoft.com", vote: 5, isRequired: true),    // Approved with suggestions (≥5)
            ("api-reviewer2@microsoft.com", vote: 10, isRequired: true),   // Approved (≥5)
            ("api-reviewer3@microsoft.com", vote: 0, isRequired: true)     // No vote (<5)
        );

        // Act
        var ratio = GetApiApprovalRatioFromService(pr);

        // Assert  
        Assert.Equal("2/3", ratio); // Both vote=5 and vote=10 count as approved out of 3 API reviewers
    }

    [Fact]
    public void GetApiApprovalRatio_WithMixedReviewerTypes_FiltersCorrectly()
    {
        // Arrange
        var pr = CreatePullRequestWithReviewers(
            ("api-reviewer1@microsoft.com", vote: 10, isRequired: true),   // API reviewer, required, approved
            ("api-reviewer2@microsoft.com", vote: 0, isRequired: false),   // API reviewer, not required
            ("non-api-reviewer@microsoft.com", vote: 10, isRequired: true) // Non-API reviewer, required, approved
        );

        // Act
        var ratio = GetApiApprovalRatioFromService(pr);

        // Assert
        Assert.Equal("1/1", ratio); // Only the required API reviewer counts
    }

    [Fact] 
    public void GetMyVoteStatus_WithNullReviewers_ReturnsNotReviewer()
    {
        // Arrange
        var pr = new GitPullRequest
        {
            PullRequestId = 12345,
            Reviewers = null // Null reviewers list
        };

        // Act
        var status = GetMyVoteStatusFromService(pr);

        // Assert
        Assert.Equal("---", status);
    }

    #region Helper Methods

    private GitPullRequest CreatePullRequestWithReviewers(params (string uniqueName, int vote, bool isRequired)[] reviewers)
    {
        var reviewerList = reviewers.Select(r => 
        {
            string reviewerId;
            if (r.uniqueName.Contains("@"))
            {
                reviewerId = Guid.NewGuid().ToString();
            }
            else
            {
                // Assume it's already a GUID string
                reviewerId = r.uniqueName;
            }

            return new IdentityRefWithVote
            {
                UniqueName = r.uniqueName,
                Id = reviewerId,
                DisplayName = r.uniqueName.Contains("@") ? r.uniqueName.Split('@')[0] : "Test User",
                Vote = (short)r.vote,  // Vote is a short, not int
                IsRequired = r.isRequired,
                IsContainer = false,
                IsFlagged = false
            };
        }).ToList();

        return new GitPullRequest
        {
            PullRequestId = 12345,
            Title = "Test PR",
            Status = PullRequestStatus.Active,
            Reviewers = reviewerList.ToArray(),
            CreationDate = DateTime.UtcNow.AddDays(-1),
            CreatedBy = new IdentityRef { DisplayName = "PR Author" }
        };
    }

    // These methods use reflection to access private methods in the service
    // This is a common pattern for testing internal logic
    private string GetApiApprovalRatioFromService(GitPullRequest pr)
    {
        var method = typeof(PullRequestAnalysisService).GetMethod("GetApiApprovalRatio", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        return (string)method!.Invoke(_service, new object[] { pr })!;
    }

    private string GetMyVoteStatusFromService(GitPullRequest pr)
    {
        var method = typeof(PullRequestAnalysisService).GetMethod("GetMyVoteStatus", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        return (string)method!.Invoke(_service, new object[] { pr })!;
    }

    private bool GetIsApprovedByCurrentUserFromService(GitPullRequest pr)
    {
        var method = typeof(PullRequestAnalysisService).GetMethod("IsApprovedByCurrentUser", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        return (bool)method!.Invoke(_service, new object[] { pr })!;
    }

    #endregion
}
