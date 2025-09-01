using System.CommandLine;
using System.CommandLine.Binding;
using gapir.Models;

namespace gapir.Infrastructure;

/// <summary>
/// Binder for global options available to all commands
/// </summary>
public class GlobalOptionsBinder : BinderBase<GlobalOptions>
{
    private readonly Option<bool> _verboseOption;
    private readonly Option<Format> _formatOption;

    public GlobalOptionsBinder(Option<bool> verboseOption, Option<Format> formatOption)
    {
        _verboseOption = verboseOption;
        _formatOption = formatOption;
    }

    protected override GlobalOptions GetBoundValue(BindingContext bindingContext)
    {
        return new GlobalOptions(
            bindingContext.ParseResult.GetValueForOption(_verboseOption),
            bindingContext.ParseResult.GetValueForOption(_formatOption)
        );
    }
}

/// <summary>
/// Binder for review command options
/// </summary>
public class ReviewOptionsBinder : BinderBase<ReviewOptions>
{
    private readonly Option<bool> _fullUrlsOption;
    private readonly Option<bool> _detailedTimingOption;
    private readonly Option<bool> _showDetailedInfoOption;

    public ReviewOptionsBinder(
        Option<bool> fullUrlsOption,
        Option<bool> detailedTimingOption,
        Option<bool> showDetailedInfoOption)
    {
        _fullUrlsOption = fullUrlsOption;
        _detailedTimingOption = detailedTimingOption;
        _showDetailedInfoOption = showDetailedInfoOption;
    }

    protected override ReviewOptions GetBoundValue(BindingContext bindingContext)
    {
        return new ReviewOptions(
            bindingContext.ParseResult.GetValueForOption(_fullUrlsOption),
            bindingContext.ParseResult.GetValueForOption(_detailedTimingOption),
            bindingContext.ParseResult.GetValueForOption(_showDetailedInfoOption)
        );
    }
}

/// <summary>
/// Binder for approved command options
/// </summary>
public class ApprovedOptionsBinder : BinderBase<ApprovedOptions>
{
    private readonly Option<bool> _fullUrlsOption;
    private readonly Option<bool> _detailedTimingOption;
    private readonly Option<bool> _showDetailedInfoOption;

    public ApprovedOptionsBinder(
        Option<bool> fullUrlsOption,
        Option<bool> detailedTimingOption,
        Option<bool> showDetailedInfoOption)
    {
        _fullUrlsOption = fullUrlsOption;
        _detailedTimingOption = detailedTimingOption;
        _showDetailedInfoOption = showDetailedInfoOption;
    }

    protected override ApprovedOptions GetBoundValue(BindingContext bindingContext)
    {
        return new ApprovedOptions(
            bindingContext.ParseResult.GetValueForOption(_fullUrlsOption),
            bindingContext.ParseResult.GetValueForOption(_detailedTimingOption),
            bindingContext.ParseResult.GetValueForOption(_showDetailedInfoOption)
        );
    }
}

/// <summary>
/// Binder for diagnose command options
/// </summary>
public class DiagnoseOptionsBinder : BinderBase<DiagnoseOptions>
{
    private readonly Argument<int> _pullRequestIdArgument;

    public DiagnoseOptionsBinder(Argument<int> pullRequestIdArgument)
    {
        _pullRequestIdArgument = pullRequestIdArgument;
    }

    protected override DiagnoseOptions GetBoundValue(BindingContext bindingContext)
    {
        return new DiagnoseOptions(
            bindingContext.ParseResult.GetValueForArgument(_pullRequestIdArgument)
        );
    }
}

/// <summary>
/// Binder for collect command options
/// </summary>
public class CollectOptionsBinder : BinderBase<CollectOptions>
{
    private readonly Option<bool> _dryRunOption;

    public CollectOptionsBinder(Option<bool> dryRunOption)
    {
        _dryRunOption = dryRunOption;
    }

    protected override CollectOptions GetBoundValue(BindingContext bindingContext)
    {
        return new CollectOptions(
            bindingContext.ParseResult.GetValueForOption(_dryRunOption)
        );
    }
}
