using System.Net;

namespace CrmIntegration.UnitTests.TestDoubles;

/// <summary>
/// Deterministic HttpMessageHandler test double: records the last request and its body, and
/// answers with whatever the test's responder function returns. Used to unit test HubSpotClient
/// without calling the real HubSpot API.
/// </summary>
public class FakeHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;

    public HttpRequestMessage? LastRequest { get; private set; }
    public string? LastRequestBody { get; private set; }
    public int CallCount { get; private set; }

    public FakeHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
    {
        _responder = responder;
    }

    public FakeHttpMessageHandler(HttpStatusCode statusCode, string? jsonBody = null)
        : this(_ => new HttpResponseMessage(statusCode)
        {
            Content = jsonBody is null ? null : new StringContent(jsonBody, System.Text.Encoding.UTF8, "application/json")
        })
    {
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        CallCount++;
        LastRequest = request;
        LastRequestBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        return _responder(request);
    }
}
