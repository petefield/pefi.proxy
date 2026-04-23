namespace PeFi.Proxy.Models;

public class PersistedRoute
{
    public string Id { get; set; } = "";
    public string RouteId { get; set; } = "";
    public string ClusterId { get; set; } = "";
    public string Host { get; set; } = "";
    public string DestinationAddress { get; set; } = "";
    public string? Path { get; set; }
}
