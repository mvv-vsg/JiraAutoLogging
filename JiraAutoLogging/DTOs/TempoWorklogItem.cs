namespace JiraAutoLogging.DTOs;

public class TempoWorklogItem
{
    public long TempoWorklogId { get; set; }
    
    public long TimeSpentSeconds { get; set; }
    
    public string? Description { get; set; }
    
    public TempoWorklogIssue Issue { get; set; }
}