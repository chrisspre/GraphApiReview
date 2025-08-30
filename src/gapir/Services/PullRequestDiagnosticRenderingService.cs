namespace gapir.Services;

using System.Text.Json;
using gapir.Models;

/// <summary>
/// Service responsible for rendering pull request diagnostic information
/// </summary>
public class PullRequestDiagnosticRenderingService
{
    private readonly Format _format;

    public PullRequestDiagnosticRenderingService(Format format)
    {
        _format = format;
    }

    public void RenderDiagnosticResult(PrDiagnosticResult result)
    {
        switch (_format)
        {
            case Format.Json:
                RenderJson(result);
                break;
            case Format.Text:
            default:
                RenderText(result);
                break;
        }
    }

    private void RenderJson(PrDiagnosticResult result)
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        var json = JsonSerializer.Serialize(result, options);
        Console.WriteLine(json);
    }

    private void RenderText(PrDiagnosticResult result)
    {
        Console.WriteLine($"Investigating PR {result.PullRequestId} reviewer details");
        Console.WriteLine("=====================================");
        
        if (!string.IsNullOrEmpty(result.ErrorMessage))
        {
            Console.WriteLine($"Error: {result.ErrorMessage}");
            return;
        }
        Console.WriteLine($"PR Title: {result.Title}");
        Console.WriteLine($"PR Status: {result.Status}");
        Console.WriteLine($"Created By: {result.CreatedBy}");
        Console.WriteLine($"Creation Date: {result.CreationDate}");
        Console.WriteLine($"Total Reviewers Count: {result.ReviewersCount}");
        Console.WriteLine();

        if (result.Reviewers.Any())
        {
            Console.WriteLine("REVIEWER DETAILS:");
            Console.WriteLine("================");

            foreach (var reviewer in result.Reviewers)
            {
                Console.WriteLine($"Reviewer: {reviewer.DisplayName}");
                Console.WriteLine($"  - Unique Name: {reviewer.UniqueName}");
                Console.WriteLine($"  - ID:          {reviewer.Id}");
                Console.WriteLine($"  - Vote:        {reviewer.Vote} ({GetVoteDescription(reviewer.Vote)})");
                Console.WriteLine($"  - IsRequired:  {reviewer.IsRequired}");
                Console.WriteLine($"  - IsContainer: {reviewer.IsContainer}");
                Console.WriteLine($"  - IsFlagged:   {reviewer.IsFlagged}");
                Console.WriteLine();
            }

            if (result.CurrentUserReviewer != null)
            {
                Console.WriteLine("YOUR REVIEWER STATUS:");
                Console.WriteLine("====================");
                Console.WriteLine("Found in reviewers list: YES");
                Console.WriteLine($"Your Vote:       {result.CurrentUserReviewer.Vote} ({GetVoteDescription(result.CurrentUserReviewer.Vote)})");
                Console.WriteLine($"IsRequired:      {result.CurrentUserReviewer.IsRequired}");
                Console.WriteLine($"IsContainer:     {result.CurrentUserReviewer.IsContainer}");
                Console.WriteLine($"IsFlagged:       {result.CurrentUserReviewer.IsFlagged}");
            }
            else
            {
                Console.WriteLine("YOUR REVIEWER STATUS: NOT FOUND in reviewers list");
            }
        }
        else
        {
            Console.WriteLine("No reviewers found for this PR");
        }
    }

    private static string GetVoteDescription(int vote)
    {
        return vote switch
        {
            10 => "Approved",
            5 => "Approved with suggestions",
            0 => "No vote",
            -5 => "Waiting for author",
            -10 => "Rejected",
            _ => "Unknown"
        };
    }
}
