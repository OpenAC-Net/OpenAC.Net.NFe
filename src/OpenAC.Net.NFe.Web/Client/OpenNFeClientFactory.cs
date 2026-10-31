using Microsoft.Extensions.Logging;
using System.Net.Http;
using OpenAC.Net.NFe.Certificates;
using Microsoft.Extensions.Options;
using OpenAC.Net.NFe.Common;
using OpenAC.Net.DFe.Core.Storage;

namespace OpenAC.Net.NFe.Web;

internal sealed class OpenNFeClientFactory(
    INFeConfigurationProvider<NFeConfig> configurations,
    INFeCertificateProvider certificates,
    INFeCertificadoProvider certificateReader,
    IHttpClientFactory clients,
    NFeHttpClientRegistry registry,
    ILogger<NFeWebHttpTransport> logger,
    IDFeDocumentStore store) : IOpenNFeClientFactory
{
    public async ValueTask<IOpenNFeClient> CriarAsync(string tenantId = "default",
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(tenantId))
            throw new ArgumentException("Empresa não informada.", nameof(tenantId));
        cancellationToken.ThrowIfCancellationRequested();
        var configuration =
            await configurations.GetConfigurationAsync(tenantId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("O provider retornou uma configuração nula.");

        configuration = configuration.Copiar();
        _ = new OpenAC.Net.DFe.Core.Http.DFeHttpTransportOptions(configuration.WebServices);
        await certificates.ConfigureAsync(tenantId, configuration.Certificados, cancellationToken)
            .ConfigureAwait(false);

        using (var certificate = await certificateReader.ObterAsync(configuration.Certificados, cancellationToken)
                   .ConfigureAwait(false))
            configuration.Certificados.ValidarCertificado(certificate);

        configuration.HttpTransport = new NFeWebHttpTransport(tenantId, clients, registry, logger);
        configuration.DocumentStore = store;
        if (configuration.Arquivos.Salvar || configuration.WebServices.Salvar)
        {
            var tenant = NFeHttpClientRegistry.Hash(tenantId);
            configuration.Arquivos.PathSalvar = Path.Combine(Path.GetFullPath(configuration.Arquivos.PathSalvar), tenant);
        }

        return new OpenNFe(configuration, certificateReader);
    }
}
