namespace JiraAutoLogging.DTOs;

public class TempoWorklogItem
{
    public long TempoWorklogId { get; set; }
    
    public long TimeSpentSeconds { get; set; }
    
    public TempoWorklogIssue Issue { get; set; }
}