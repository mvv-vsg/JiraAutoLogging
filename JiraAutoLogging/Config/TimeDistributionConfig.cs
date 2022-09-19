namespace JiraAutoLogging.Config;

// Time distribution based on number of tasks in progress
// 1 task = 5 minutes
// 2 tasks = 1st task gets 3 minutes, 2nd task gets 2 minutes
// 3 tasks = 1st 2 mins, 2nd 2 mins, 3rd 1 min
// etc...
public class TimeDistributionConfig : Dictionary<string, int[]>
{
}