using System.Globalization;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using OpenAC.Net.DFe.Core.Extensions;
using OpenAC.Net.NFe.Common;
using OpenAC.Net.NFe.Common.Model;
using OpenAC.Net.NFe.Common.Types;

namespace OpenAC.Net.NFe.Services.DistribuicaoDFe;

public class NFeDistribuicaoServiceClient(NFeConfig config, X509Certificate2 certificado)
    : NFeServiceClient<NFeDistribuicaoResult, NFeDistribuicaoResposta>(config, TipoServicoNFe.NFeDistribuicaoDFe, SchemaNFe.DistDFeInt, "retDistDFeInt", certificado)
{
    public Task<NFeDistribuicaoResposta> ConsultarAsync(NFeDistribuicaoSolicitacao pedido, CancellationToken token = default) =>
        EnviarAsync(new NFeDistribuicao
        {
            TpAmb = Ambiente, CUFAutor = ((int)Configuracoes.WebServices.UF.ToCodeUf()).ToString(CultureInfo.InvariantCulture),
            CNPJ = pedido.CpfCnpj.Length == 14 ? pedido.CpfCnpj : null,
            CPF = pedido.CpfCnpj.Length == 11 ? pedido.CpfCnpj : null,
            ConsChNFe = pedido.Chave == null ? null : new NFeConsChNFe { Chave = pedido.Chave },
            ConsNSU = pedido.Nsu == null ? null : new NFeConsNSU { NSU = pedido.Nsu },
            DistNSU = pedido.UltimoNsu == null ? null : new NFeDistNSU { UltNSU = pedido.UltimoNsu }
        }, token);

    protected override NFeDistribuicaoResposta CriarResposta(string xmlEnvio, string xmlRetorno) =>
        new(xmlEnvio, xmlRetorno, EnvelopeEnvio, EnvelopeRetorno);
}
