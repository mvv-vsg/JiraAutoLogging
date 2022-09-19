namespace JiraAutoLogging.DTOs;

public class JiraTaskListResponse
{
    public List<JiraIssue> Issues { get; set; } = new();
}