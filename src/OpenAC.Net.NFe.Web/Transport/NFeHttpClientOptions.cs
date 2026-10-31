using System.Net.Http;
using Microsoft.Extensions.Http.Resilience;
using Polly;
using System.Net;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Options;

namespace OpenAC.Net.NFe.Web;

internal sealed class NFeHttpClientOptions(NFeHttpClientRegistry registry)
    : IConfigureNamedOptions<HttpClientFactoryOptions>
{
    public void Configure(HttpClientFactoryOptions value)
    {
    }

    public void Configure(string? name, HttpClientFactoryOptions value)
    {
        if (name == null || !registry.TryGet(name, out var registration)) return;
        value.HandlerLifetime = registration.Options.TempoVidaHandler;
        value.HttpClientActions.Add(client => client.Timeout = Timeout.InfiniteTimeSpan);
        value.HttpMessageHandlerBuilderActions.Add(builder =>
        {
#if NET8_0_OR_GREATER
            builder.PrimaryHandler = new SocketsHttpHandler
            {
                AllowAutoRedirect = false,
                UseCookies = false,
                AutomaticDecompression = DecompressionMethods.All,
                PooledConnectionLifetime = registration.Options.TempoVidaConexaoPool,
                SslOptions = new SslClientAuthenticationOptions
                {
                    EnabledSslProtocols = registration.Protocols,
                    ClientCertificates = new X509CertificateCollection { registration.Certificate }
                }
            };
#else
            var handler = new HttpClientHandler
            {
                AllowAutoRedirect = false,
                UseCookies = false,
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
                ClientCertificateOptions = ClientCertificateOption.Manual
            };
            handler.ClientCertificates.Add(registration.Certificate);
#if NETFRAMEWORK
            typeof(HttpClientHandler).GetProperty("SslProtocols")?.SetValue(handler, registration.Protocols);
#else
            handler.SslProtocols = registration.Protocols;
#endif
            builder.PrimaryHandler = handler;
#endif
            builder.AdditionalHandlers.Add(new ResilienceHandler(
                OpenAC.Net.DFe.Core.Http.DFeHttpResilience.CreatePipeline(registration.Options)));
        });
    }
}
