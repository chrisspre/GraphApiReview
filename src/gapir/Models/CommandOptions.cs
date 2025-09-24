namespace gapir.Models;

/// <summary>
/// Global options available to all commands
/// </summary>
public record GlobalOptions(bool Verbose, Format Format);

/// <summary>
/// Options specific to the review command
/// </summary>
public record ReviewOptions(
    bool DetailedTiming, 
    bool ShowDetailedInfo);

/// <summary>
/// Options specific to the approved command
/// </summary>
public record ApprovedOptions(
    bool DetailedTiming, 
    bool ShowDetailedInfo);

/// <summary>
/// Options specific to the completed command
/// </summary>
public record CompletedOptions(
    bool DetailedTiming, 
    bool ShowDetailedInfo,
    int DaysBack = 30);

/// <summary>
/// Options specific to the diagnose command
/// </summary>
public record DiagnoseOptions(int PullRequestId);

/// <summary>
/// Options specific to the collect command
/// </summary>
public record CollectOptions(bool DryRun);
