using Newtonsoft.Json.Linq;

namespace JiraAutoLogging.DTOs;

public class TempoWorklogResponse
{
    public List<TempoWorklogItem> Results { get; set; } = new();
}