using System.Net.Http;
using Polly;
using Polly.Registry;

namespace CurrencyConverter.Core.Infrastructure.Http;

public interface IHttpClient
{
    Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken = default);
}

public class ResilientHttpClient : IHttpClient
{
    private readonly HttpClient _httpClient;
    private readonly IAsyncPolicy<HttpResponseMessage> _policy;

    public ResilientHttpClient(HttpClient httpClient, IReadOnlyPolicyRegistry<string> policyRegistry)
    {
        _httpClient = httpClient;
        _policy = policyRegistry.Get<IAsyncPolicy<HttpResponseMessage>>("CombinedPolicy");
    }

    public async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken = default)
    {
        return await _policy.ExecuteAsync(async ct =>
        {
            using var clonedRequest = await CloneHttpRequestMessageAsync(request);
            return await _httpClient.SendAsync(clonedRequest, ct);
        }, cancellationToken);
    }

    private static async Task<HttpRequestMessage> CloneHttpRequestMessageAsync(HttpRequestMessage request)
    {
        var clone = new HttpRequestMessage(request.Method, request.RequestUri)
        {
            Version = request.Version
        };

        // Copy the content (if any)
        if (request.Content != null)
        {
            var ms = new MemoryStream();
            await request.Content.CopyToAsync(ms);
            ms.Position = 0;
            clone.Content = new StreamContent(ms);
            // Copy headers from original content
            foreach (var header in request.Content.Headers)
                clone.Content.Headers.Add(header.Key, header.Value);
        }

        // Copy the headers
        foreach (var header in request.Headers)
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);

        // Copy the properties
        foreach (var prop in request.Options)
            clone.Options.Set(new HttpRequestOptionsKey<object?>(prop.Key), prop.Value);

        return clone;
    }
}