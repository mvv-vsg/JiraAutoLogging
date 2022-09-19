namespace JiraAutoLogging.Config;

public class ServicesConfig
{
    public string MicrosoftClientId { get; set; }
    
    public string MicrosoftClientSecret { get; set; }
    
    public string MicrosoftTenantId { get; set; }
    
    public string TempoAuthKey { get; set; }
    
    public string TempoAccountId { get; set; }
    
    public string JiraAuthToken { get; set; }
    
    public string JiraEmail { get; set; }
    
    public string JiraFilter { get; set; }
}