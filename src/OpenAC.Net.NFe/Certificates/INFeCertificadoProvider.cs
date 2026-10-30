using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using OpenAC.Net.NFe.Common;

namespace OpenAC.Net.NFe.Certificates;

public interface INFeCertificadoProvider
{
    /// <summary>Retorna uma instância própria por operação. O cliente a descarta ao concluir.</summary>
    Task<X509Certificate2> ObterAsync(NFeConfigCertificados configuracao, CancellationToken cancellationToken = default);
}
