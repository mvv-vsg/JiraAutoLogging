using JiraAutoLogging.Service;
using Microsoft.AspNetCore.Mvc;

namespace JiraAutoLogging.Controllers;

[ApiController]
[Route("[controller]")]
public class RedirectController
{

    private readonly WorkerService _workerService;

    public RedirectController(WorkerService workerService)
    {
        _workerService = workerService;
    }

    [HttpGet]
    [Route("/redirect")]
    public async Task<string> Redirect(
        [FromQuery] string code, 
        [FromQuery(Name = "client_info")] string clientInfo,
        [FromQuery(Name = "session_state")] string sessionState
    )
    {
        await _workerService.AuthenticateWithMicrosoft(code);

        return "You may close the browser window now.";
    }

    [HttpGet]
    [Route("/FetchMicrosoftAuthUrl")]
    public async Task<string> FetchMicrosoftAuthUrl()
    {
        return (await _workerService.FetchAuthURL()).ToString();
    }

    [HttpGet]
    [Route("/GetPresence")]
    public async Task<string> GetPresence()
    {
        return (await _workerService.FetchMicrosoftPresence()).Availability;
    }
}