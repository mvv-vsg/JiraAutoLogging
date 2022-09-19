using JiraAutoLogging.Config;
using JiraAutoLogging.Service;

namespace JiraAutoLogging.Worker;

public class Worker : BackgroundService
{
    private const int Delay = 300000; // 5 minutes in millis
    
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

                    await _workerService.LogTimeForKey(keys, TimeSpan.FromMilliseconds(Delay));
                }
                else
                {
                    await _workerService.LogTimeForKey(new List<string> {"IP-24"}, TimeSpan.FromMilliseconds(Delay));
                }
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Error occured");
            }

            await Task.Delay(Delay, stoppingToken);
        }
    }
}