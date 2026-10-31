using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using OpenAC.Net.DFe.Core.Http;

namespace OpenAC.Net.NFe.Web;

internal sealed class NFeHttpRegistration(X509Certificate2 certificate, DFeHttpTransportOptions options)
{
    internal X509Certificate2 Certificate { get; } = certificate;
    internal SslProtocols Protocols => Options.Protocols;
    internal DFeHttpTransportOptions Options { get; } = options;
}
