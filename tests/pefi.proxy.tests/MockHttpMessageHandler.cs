using System.Net;
using System.Net.Http.Json;
using pefi.dynamicdns.Services;

namespace PeFi.Proxy.Tests;

/// <summary>
/// A mock HttpMessageHandler that returns a predefined list of <see cref="GetServiceResponse"/> objects.
/// </summary>
public class MockHttpMessageHandler(IEnumerable<GetServiceResponse> services) : HttpMessageHandler
{
    private readonly List<GetServiceResponse> _services = services.ToList();
    public int RequestCount { get; private set; }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        RequestCount++;
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(_services)
        };
        return Task.FromResult(response);
    }
}
