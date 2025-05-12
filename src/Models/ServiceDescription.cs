using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson;

namespace PeFi.Proxy;

public class ServiceDescription(
    string ServiceName, string? HostName, string? ContainerPortNumber, string? HostPortNumber)
{

    [BsonId]
    public ObjectId Id { get; set; }

    [BsonElement("ServiceName")]
    public string ServiceName { get; set; } = ServiceName;

    [BsonElement("HostName")]
    public string? HostName { get; set; } = HostName;

    [BsonElement("ContainerPortNumber")]
    public string? ContainerPortNumber { get; set; } = ContainerPortNumber;

    [BsonElement("HostPortNumber")]
    public string? HostPortNumber { get; set; } = HostPortNumber;
}
