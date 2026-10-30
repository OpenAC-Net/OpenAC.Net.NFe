using System.Globalization;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using OpenAC.Net.DFe.Core.Extensions;
using OpenAC.Net.NFe.Common;
using OpenAC.Net.NFe.Common.Model;
using OpenAC.Net.NFe.Common.Types;

namespace OpenAC.Net.NFe.Services.Inutilizacao;

public class NFeInutilizacaoServiceClient(NFeConfig config, X509Certificate2 certificado)
    : NFeServiceClient<NFeInutilizacaoResult, NFeInutilizacaoResposta>(config, TipoServicoNFe.NFeInutilizacao, SchemaNFe.InutNFe, "retInutNFe", certificado)
{
    public async Task<NFeInutilizacaoResposta> InutilizarAsync(NFeInutilizacaoSolicitacao pedido, CancellationToken token = default)
    {
        var uf = ((int)Configuracoes.WebServices.UF.ToCodeUf()).ToString(CultureInfo.InvariantCulture);
        var id = "ID" + uf + pedido.Ano.ToString("D2", CultureInfo.InvariantCulture) + pedido.Cnpj +
            (int)Configuracoes.Geral.Modelo + pedido.Serie.ToString("D3", CultureInfo.InvariantCulture) +
            pedido.NumeroInicial.ToString("D9", CultureInfo.InvariantCulture) + pedido.NumeroFinal.ToString("D9", CultureInfo.InvariantCulture);
        var documento = new NFeInutilizacao
        {
            InfInut = new NFeInutilizacaoInfo
            {
                Id = id, TpAmb = Ambiente, CUF = uf, Ano = pedido.Ano.ToString("D2", CultureInfo.InvariantCulture),
                CNPJ = pedido.Cnpj, Modelo = ((int)Configuracoes.Geral.Modelo).ToString(CultureInfo.InvariantCulture),
                Serie = pedido.Serie.ToString(CultureInfo.InvariantCulture), NumeroInicial = pedido.NumeroInicial.ToString(CultureInfo.InvariantCulture),
                NumeroFinal = pedido.NumeroFinal.ToString(CultureInfo.InvariantCulture), Justificativa = pedido.Justificativa
            }
        };
        documento.Assinar(Certificado);
        var resposta = await EnviarAsync(documento, token).ConfigureAwait(false);
        if (resposta.CStat == 102)
            resposta.Documento = new NFeProcInutilizacao { Inutilizacao = documento, Retorno = resposta.Resultado };
        return resposta;
    }

    protected override NFeInutilizacaoResposta CriarResposta(string xmlEnvio, string xmlRetorno) =>
        new(xmlEnvio, xmlRetorno, EnvelopeEnvio, EnvelopeRetorno);
}
