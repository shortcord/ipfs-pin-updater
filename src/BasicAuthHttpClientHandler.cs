using System.Text;
using System.Net.Http.Headers;

namespace ipfs_pin_util;

public class BasicAuthHttpClientHandler : HttpClientHandler
{
    readonly string Token;

    public BasicAuthHttpClientHandler(string username, string password)
    {
        Token = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{username}:{password}"));
    }

    protected override HttpResponseMessage Send(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", Token);
        return base.Send(request, cancellationToken);
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", Token);
        return base.SendAsync(request, cancellationToken);
    }
}