namespace PeFi.Proxy.Models;

public class PersistedCluster
{
    public string Id { get; set; } = "";
    public string ClusterId { get; set; } = "";
    public Dictionary<string, string> Destinations { get; set; } = new();
}
