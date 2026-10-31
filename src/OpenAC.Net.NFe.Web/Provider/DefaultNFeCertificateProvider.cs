using OpenAC.Net.NFe.Common;

namespace OpenAC.Net.NFe.Web;

internal sealed class DefaultNFeCertificateProvider : INFeCertificateProvider
{
    public ValueTask ConfigureAsync(string tenantId, NFeConfigCertificados configuration,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return default;
    }
}
