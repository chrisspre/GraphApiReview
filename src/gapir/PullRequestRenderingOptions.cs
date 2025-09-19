namespace gapir;

public class PullRequestRenderingOptions
{
    // public bool ShowApproved { get; set; } = false;
    public bool ShowDetailedTiming { get; set; } = false;
    public bool ShowDetailedInfo { get; set; } = false;
    // public bool ShowLegend { get; set; } = true;
    public Format Format { get; set; } = Format.Text;
}
