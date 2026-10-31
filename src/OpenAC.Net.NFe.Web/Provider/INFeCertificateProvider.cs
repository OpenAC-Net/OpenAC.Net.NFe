using OpenAC.Net.NFe.Common;

namespace OpenAC.Net.NFe.Web;

/// <summary>Permite obter ou renovar o certificado sem acoplar a biblioteca ao repositório de segredos.</summary>
public interface INFeCertificateProvider
{
    ValueTask ConfigureAsync(string tenantId, NFeConfigCertificados configuration,
        CancellationToken cancellationToken = default);
}
