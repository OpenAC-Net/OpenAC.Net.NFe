using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using OpenAC.Net.NFe.Common;
using OpenAC.Net.NFe.Common.Model;
using OpenAC.Net.NFe.Common.Types;

namespace OpenAC.Net.NFe.Services.Consulta;

public class NFeConsultaServiceClient(NFeConfig config, X509Certificate2 certificado)
    : NFeServiceClient<NFeConsultaResult, NFeConsultaResposta>(config, TipoServicoNFe.NFeConsultaProtocolo, SchemaNFe.ConsSitNFe, "retConsSitNFe", certificado)
{
    public Task<NFeConsultaResposta> ConsultarAsync(string chave, CancellationToken token = default) =>
        EnviarAsync(new NFeConsultaSituacao { TpAmb = Ambiente, Chave = chave }, token);

    protected override NFeConsultaResposta CriarResposta(string xmlEnvio, string xmlRetorno) =>
        new(xmlEnvio, xmlRetorno, EnvelopeEnvio, EnvelopeRetorno);
}
