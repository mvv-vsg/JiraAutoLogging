using JiraAutoLogging.Config;
using JiraAutoLogging.Service;

namespace JiraAutoLogging.Worker;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly WorkerService _workerService;
    private readonly TimeLoggingConfig _timeLogging;
    
    public Worker(WorkerService workerService, ILogger<Worker> logger, TimeLoggingConfig timeLogging)
    {
        _workerService = workerService;
        _logger = logger;
        _timeLogging = timeLogging;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var presence = await _workerService.FetchMicrosoftPresence();

                if (new[] {DayOfWeek.Saturday, DayOfWeek.Sunday}.Contains(DateTime.Now.DayOfWeek))
                {
                    continue;
                }
                
                if ((TimeSpan.Parse(_timeLogging.StartTime) > DateTime.Now.TimeOfDay ||
                    TimeSpan.Parse(_timeLogging.EndTime) < DateTime.Now.TimeOfDay))
                {
                    continue;
                }
                
                if (_timeLogging.EnableBreak &&
                    TimeSpan.Parse(_timeLogging.BreakStartTime) < DateTime.Now.TimeOfDay &&
                    TimeSpan.Parse(_timeLogging.BreakEndTime) > DateTime.Now.TimeOfDay)
                {
                    continue;
                }

                
                if (presence.Availability != "Busy")
                {
                    var keys = await _workerService.GetListOfTasksInProgress();

                    if (keys.Count > 0)
                    {
                        await _workerService.LogTimeForKey(keys, TimeSpan.FromMilliseconds(FetchTimeLoggingDelay()));
                    }
                    else
                    {
                        await _workerService.LogTimeForKey(new List<string> {_timeLogging.IdleTask}, TimeSpan.FromMilliseconds(FetchTimeLoggingDelay()));
                    }
                }
                else
                {
                    await _workerService.LogTimeForKey(new List<string> {_timeLogging.MeetingTask}, TimeSpan.FromMilliseconds(FetchTimeLoggingDelay()));
                }
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Error occured");
            }

            await Task.Delay(FetchTimeLoggingDelay(), stoppingToken);
        }
    }

    private int FetchTimeLoggingDelay()
    {
        return (int) TimeSpan.FromMinutes(_timeLogging.TickTimeInMinutes).TotalMilliseconds;
    }
}