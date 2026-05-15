namespace PicoBusX.Web.Models;

public class RuleInfo
{
    public string Name { get; set; } = string.Empty;
    public string FilterType { get; set; } = string.Empty;
    public string? FilterExpression { get; set; }
    public string? ActionExpression { get; set; }
}
