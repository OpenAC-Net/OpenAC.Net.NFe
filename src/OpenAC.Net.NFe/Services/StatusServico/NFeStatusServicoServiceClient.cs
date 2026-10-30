using System.Globalization;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using OpenAC.Net.DFe.Core.Extensions;
using OpenAC.Net.NFe.Common;
using OpenAC.Net.NFe.Common.Model;
using OpenAC.Net.NFe.Common.Types;

namespace OpenAC.Net.NFe.Services.StatusServico;

public class NFeStatusServicoServiceClient(NFeConfig config, X509Certificate2 certificado)
    : NFeServiceClient<NFeStatusServicoResult, NFeStatusServicoResposta>(config, TipoServicoNFe.NFeStatusServico, SchemaNFe.ConsStatServ, "retConsStatServ", certificado)
{
    public Task<NFeStatusServicoResposta> ConsultarAsync(CancellationToken token = default) =>
        EnviarAsync(new NFeConsultaStatus
        { TpAmb = Ambiente, CUF = ((int)Configuracoes.WebServices.UF.ToCodeUf()).ToString(CultureInfo.InvariantCulture) }, token);

    protected override NFeStatusServicoResposta CriarResposta(string xmlEnvio, string xmlRetorno) =>
        new(xmlEnvio, xmlRetorno, EnvelopeEnvio, EnvelopeRetorno);
}
