using System.Net.Http;
using Polly.CircuitBreaker;
using Polly.Timeout;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenAC.Net.DFe.Core.Http;

namespace OpenAC.Net.NFe.Web;

internal sealed class NFeWebHttpTransport(
    string tenantId,
    IHttpClientFactory clients,
    NFeHttpClientRegistry registry,
    ILogger<NFeWebHttpTransport> logger) : IDFeHttpTransport
{
    private static readonly ActivitySource Activities = new("OpenAC.Net.NFe.Web");
    private static readonly Meter Meter = new("OpenAC.Net.NFe.Web");
    private static readonly Counter<long> Count = Meter.CreateCounter<long>("openac.nfe.requests");

    private static readonly Histogram<double> Duration =
        Meter.CreateHistogram<double>("openac.nfe.request.duration", "ms");

    public async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
        DFeHttpTransportOptions transportOptions,
        CancellationToken cancellationToken = default)
    {
        var endpoint = request.RequestUri ?? throw new ArgumentException("Endpoint não informado.");
        if (!endpoint.IsAbsoluteUri || endpoint.Scheme != Uri.UriSchemeHttps)
            throw new ArgumentException("O endpoint fiscal deve utilizar HTTPS.");

        var certificate = transportOptions.Certificate
                          ?? throw new InvalidOperationException("Certificado com chave privada não informado.");

        OpenAC.Net.DFe.Core.Common.DFeCertificadosConfigBase.ValidarCertificado(certificate, transportOptions.MargemValidadeCertificado);

        var name = registry.Register(tenantId, transportOptions, endpoint);
        using var activity = Activities.StartActivity("NFe.SOAP", ActivityKind.Client);
        activity?.SetTag("server.address", endpoint.Host);
        activity?.SetTag("openac.tenant", NFeHttpClientRegistry.Hash(tenantId));

        var watch = Stopwatch.StartNew();

        try
        {
            using var client = clients.CreateClient(name);
            var response = await client.SendAsync(request, HttpCompletionOption.ResponseContentRead, cancellationToken)
                .ConfigureAwait(false);
            activity?.SetTag("http.response.status_code", (int)response.StatusCode);
            Count.Add(1,
                new KeyValuePair<string, object?>("outcome", response.IsSuccessStatusCode ? "success" : "http_error"));
            return response;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (exception is HttpRequestException or OperationCanceledException
                                              or BrokenCircuitException or TimeoutRejectedException)
        {
            activity?.SetStatus(ActivityStatusCode.Error);
            Count.Add(1, new KeyValuePair<string, object?>("outcome", "transport_error"));
            logger.LogError(exception, "Falha SOAP NF-e para {Endpoint}, empresa {Tenant}", endpoint.Authority,
                NFeHttpClientRegistry.Hash(tenantId));
            throw new NFeTechnicalException("Falha no transporte SOAP da NF-e.", exception);
        }
        finally
        {
            Duration.Record(watch.Elapsed.TotalMilliseconds);
        }
    }

}
