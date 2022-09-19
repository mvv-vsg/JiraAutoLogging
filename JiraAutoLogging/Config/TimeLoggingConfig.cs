namespace JiraAutoLogging.Config;

// "TimeLogging": {
// "StartTime": "9:00",
// "EndTime": "18:00",
// "EnableBreak": true,
// "BreakStartTime": "12:00",
// "BreakEndTime": "13:00"
// }

public class TimeLoggingConfig
{
    public string StartTime { get; set; }
    
    public string EndTime { get; set; }
    
    public bool EnableBreak { get; set; }
    
    public string BreakStartTime { get; set; }
    
    public string BreakEndTime { get; set; }
}