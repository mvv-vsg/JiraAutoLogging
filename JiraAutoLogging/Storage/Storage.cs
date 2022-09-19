namespace JiraAutoLogging.Storage;

public class Storage
{
    public string AccessToken { get; set; }
    
    public DateTime AccessTokenExpiresAt { get; set; }
    
    public string RefreshToken { get; set; }
}