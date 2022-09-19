using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using JiraAutoLogging.Config;
using JiraAutoLogging.DTOs;
using Microsoft.Graph;
using Microsoft.Identity.Client;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using File = System.IO.File;

namespace JiraAutoLogging.Service;

public class WorkerService
{
    private const string StorageFileName = "storage.json";

    private readonly ServicesConfig _servicesConfig;
    
    private readonly ILogger<WorkerService> _logger;
    
    private readonly IConfidentialClientApplication _confidentialClient;
    
    private readonly HttpClient _msAuthClient = new();
    private readonly HttpClient _jiraClient;
    private readonly HttpClient _tempoClient;

    private readonly GraphServiceClient _graphServiceClient;

    private readonly IEnumerable<string> _scopes = new[] { "Presence.Read", "Presence.Read.All", "offline_access" };

    public WorkerService(ILogger<WorkerService> logger, ServicesConfig servicesConfig)
    {
        _logger = logger;
        _servicesConfig = servicesConfig;

        _confidentialClient = ConfidentialClientApplicationBuilder.CreateWithApplicationOptions(new ConfidentialClientApplicationOptions()
            {
                ClientId = servicesConfig.MicrosoftClientId,
                ClientSecret = servicesConfig.MicrosoftClientSecret
            })
            .WithAuthority($"https://login.microsoftonline.com/{servicesConfig.MicrosoftTenantId}")
            .WithRedirectUri("http://localhost:9878/redirect")
            .Build();
        
        _jiraClient = new HttpClient()
        {
            DefaultRequestHeaders =
            {
                Authorization = new AuthenticationHeaderValue(
                    AuthenticationSchemes.Basic.ToString(),
                    Convert.ToBase64String(Encoding.UTF8.GetBytes($"{servicesConfig.JiraEmail}:{servicesConfig.JiraAuthToken}"))
                )
            }
        };

        _tempoClient = new HttpClient()
        {
            DefaultRequestHeaders =
            {
                Authorization = new AuthenticationHeaderValue(
                    "Bearer",
                    servicesConfig.TempoAuthKey
                )
            }
        };
        
        _graphServiceClient = new GraphServiceClient(new DelegateAuthenticationProvider(async req =>
        {
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await FetchAccessToken());
        }));
    }

    public async Task AuthenticateWithMicrosoft(string code)
    {
        var msAuthReq = new HttpRequestMessage()
        {
            Method = HttpMethod.Post,
            RequestUri = new UriBuilder($"https://login.microsoftonline.com/{_servicesConfig.MicrosoftTenantId}/oauth2/v2.0/token").Uri,
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                {"client_id", _servicesConfig.MicrosoftClientId},
                {"client_secret", _servicesConfig.MicrosoftClientSecret},
                {"scope", "offline_access openid Presence.Read Presence.Read.All profile"},
                {"client_info", "1"},
                {"grant_type", "authorization_code"},
                {"code", code},
                {"redirect_uri", "http://localhost:9878/redirect"}
            })
        };

        var resp = JsonConvert.DeserializeObject<JObject>(await (await _msAuthClient.SendAsync(msAuthReq)).Content.ReadAsStringAsync());

        if (resp == null)
        {
            throw new Exception("Auth response was null???");
        }
        
        var accessToken = resp["access_token"]?.Value<string>();
        var refreshToken = resp["refresh_token"]?.Value<string>();
        var expiresIn = resp["expires_in"]?.Value<int>();

        if (accessToken == null || refreshToken == null || expiresIn == null)
        {
            throw new Exception("Auth tokens were null??");
        }
        
        SaveAccessToken(accessToken, refreshToken, expiresIn.Value);
    }

    private void SaveAccessToken(string accessToken, string refreshToken, int expiresIn)
    {
        var storage = FetchStorage();

        storage.AccessToken = accessToken;
        storage.RefreshToken = refreshToken;
        storage.AccessTokenExpiresAt = DateTime.Now.Add(TimeSpan.FromSeconds(expiresIn));
        
        SaveStorage(storage);
    }

    private Storage.Storage FetchStorage()
    {
        if (!File.Exists(StorageFileName))
        {
            var fileStream = File.Create(StorageFileName);
            fileStream.Close();
        }

        var storage = File.ReadAllText(StorageFileName);

        if (string.IsNullOrEmpty(storage))
        {
            return new Storage.Storage();
        }
        
        return JsonConvert.DeserializeObject<Storage.Storage>(storage) ?? throw new InvalidOperationException();
    }

    private void SaveStorage(Storage.Storage storage)
    {
        var str = JsonConvert.SerializeObject(storage);
        File.WriteAllText(StorageFileName, str);
    }
    
    private async Task<string> FetchAccessToken()
    {
        var storage = FetchStorage();

        if (string.IsNullOrEmpty(storage.AccessToken))
        {
            var url = await FetchAuthURL();
            System.Diagnostics.Process.Start(new ProcessStartInfo() { FileName = url.ToString(), UseShellExecute = true});
            
            throw new Exception("Authenticate first.");
        }

        if (storage.AccessTokenExpiresAt < DateTime.Now)
        {
            var msAuthReq = new HttpRequestMessage()
            {
                Method = HttpMethod.Post,
                RequestUri = new UriBuilder($"https://login.microsoftonline.com/{_servicesConfig.MicrosoftTenantId}/oauth2/v2.0/token").Uri,
                Content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    {"client_id", _servicesConfig.MicrosoftClientId},
                    {"client_secret", _servicesConfig.MicrosoftClientSecret},
                    {"grant_type", "refresh_token"},
                    {"refresh_token", storage.RefreshToken}
                })
            };

            var resp = JsonConvert.DeserializeObject<JObject>(await (await _msAuthClient.SendAsync(msAuthReq)).Content.ReadAsStringAsync());

            if (resp?["error"] != null)
            {
                var url = await FetchAuthURL();
                System.Diagnostics.Process.Start(new ProcessStartInfo() { FileName = url.ToString(), UseShellExecute = true});

                throw new Exception("Refresh token was unable to acquire a new access token. Re-authentication necessary.");
            }

            storage.AccessToken = resp["access_token"].Value<string>();
            storage.RefreshToken = resp["refresh_token"].Value<string>();
            storage.AccessTokenExpiresAt = DateTime.Now.Add(TimeSpan.FromSeconds(resp["expires_in"].Value<int>()));
        }

        SaveStorage(storage);
        
        return storage.AccessToken;
    }

    public async Task<Uri> FetchAuthURL()
    {
        return await _confidentialClient.GetAuthorizationRequestUrl(_scopes).ExecuteAsync();
    }

    public async Task<List<string>> GetListOfTasksInProgress()
    {
        var jiraReq = new HttpRequestMessage()
        {
            Method = HttpMethod.Get,
            RequestUri = new UriBuilder($"https://clcsdevelopment.atlassian.net/rest/api/2/search?jql={_servicesConfig.JiraFilter}").Uri
        };
        
        var jiraResult = JsonConvert.DeserializeObject<JiraTaskListResponse>(await (await _jiraClient.SendAsync(jiraReq)).Content.ReadAsStringAsync());

        if (jiraResult != null && jiraResult.Issues.Count >= 1)
        {
            return jiraResult.Issues.Select(i => i.Key).ToList();
        }
        
        _logger.LogWarning("No issues in progress");
        return new List<string>();
    }

    public async Task<Presence> FetchMicrosoftPresence()
    {
        // Available
        // Busy
        return await _graphServiceClient.Me.Presence.Request().GetAsync();
    }

    public async Task LogTimeForKey(List<string> keys, TimeSpan timespan)
    {
        if (keys.Count < 1)
        {
            return;
        }
        
        var tempoReq = new HttpRequestMessage()
        {
            Method = HttpMethod.Get,
            RequestUri = new UriBuilder($"https://api.tempo.io/4/worklogs/user/{_servicesConfig.TempoAccountId}?from={DateTime.Today.ToString("yyyy-MM-dd")}&to={DateTime.Today.ToString("yyyy-MM-dd")}").Uri
        };

        var strResult = await (await _tempoClient.SendAsync(tempoReq)).Content.ReadAsStringAsync();
        
        var tempoResult = JsonConvert.DeserializeObject<TempoWorklogResponse>(strResult);

        if (tempoResult == null)
        {
            _logger.LogWarning("Tempo result returned null");
            return;
        }

        foreach (var key in keys)
        {
            var timeToLog = timespan.TotalSeconds / (keys.Count < 1 ? 1 : keys.Count);
            
            var worklogItemId = tempoResult.Results.FirstOrDefault(wl => wl.Issue.Key == key);
            
            // if an item doesn't exist yet, create a new one
            if (worklogItemId == null)
            {
                var tempoCreateWorklogRequest = new HttpRequestMessage()
                {
                    Method = HttpMethod.Post,
                    RequestUri = new UriBuilder($"https://api.tempo.io/4/worklogs").Uri,
                    Content = JsonContent.Create(new
                    {
                        AuthorAccountId = _servicesConfig.TempoAccountId,
                        IssueKey = key,
                        StartDate = DateTime.Now.ToString("yyyy-MM-dd"),
                        TimeSpentSeconds = timeToLog
                    })
                };

                var result = await (await _tempoClient.SendAsync(tempoCreateWorklogRequest)).Content.ReadAsStringAsync();
            }
            else // if an item already exists, update it
            {
                var tempoUpdateWorklogRequest = new HttpRequestMessage()
                {
                    Method = HttpMethod.Put,
                    RequestUri = new UriBuilder($"https://api.tempo.io/4/worklogs/{worklogItemId.TempoWorklogId}").Uri,
                    Content = JsonContent.Create(new
                    {
                        AuthorAccountId = _servicesConfig.TempoAccountId,
                        IssueKey = key,
                        StartDate = DateTime.Now.ToString("yyyy-MM-dd"),
                        TimeSpentSeconds = worklogItemId.TimeSpentSeconds + timeToLog
                    })
                };
            
                var result = await (await _tempoClient.SendAsync(tempoUpdateWorklogRequest)).Content.ReadAsStringAsync();
            }
        }
        
        _logger.LogInformation("Logged");
    }
}