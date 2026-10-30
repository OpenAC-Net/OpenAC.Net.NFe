using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using OpenAC.Net.NFe.Common;
using OpenAC.Net.NFe.Common.Model;
using OpenAC.Net.NFe.Common.Types;

namespace OpenAC.Net.NFe.Services.RetRecepcao;

public class NFeRetAutorizacaoServiceClient(NFeConfig config, X509Certificate2 certificado)
    : NFeServiceClient<NFeReciboResult, NFeReciboResposta>(config, TipoServicoNFe.NFeRetAutorizacao, SchemaNFe.ConsReciNFe, "retConsReciNFe", certificado)
{
    public Task<NFeReciboResposta> ConsultarAsync(string recibo, CancellationToken token = default) =>
        EnviarAsync(new NFeConsultaRecibo { TpAmb = Ambiente, Recibo = recibo }, token);

    protected override NFeReciboResposta CriarResposta(string xmlEnvio, string xmlRetorno) =>
        new(xmlEnvio, xmlRetorno, EnvelopeEnvio, EnvelopeRetorno);
}
