using System.Threading;
using System.Threading.Tasks;
using OpenAC.Net.NFe.Common.Model;

namespace OpenAC.Net.NFe;

/// <summary>Operações independentes com dados e cancelamento explícitos por chamada.</summary>
public interface IOpenNFeClient
{
    Task<NFeAutorizacaoResposta> EnviarAsync(NFeEnvioSolicitacao solicitacao, CancellationToken cancellationToken = default);
    Task<NFeConsultaResposta> ConsultarAsync(string chave, CancellationToken cancellationToken = default);
    Task<NFeStatusServicoResposta> ConsultarSituacaoServicoAsync(CancellationToken cancellationToken = default);
    Task<NFeReciboResposta> ConsultaReciboAsync(NFeReciboSolicitacao solicitacao, CancellationToken cancellationToken = default);
    Task<NFeEventoResposta> EnviarEventoAsync(NFeEventoSolicitacao solicitacao, CancellationToken cancellationToken = default);
    Task<NFeCadastroResposta> ConsultaCadastroAsync(string documento, string tipo = "CNPJ", CancellationToken cancellationToken = default);
    Task<NFeInutilizacaoResposta> InutilizarAsync(NFeInutilizacaoSolicitacao solicitacao, CancellationToken cancellationToken = default);
    Task<NFeDistribuicaoResposta> DistribuicaoDFeAsync(NFeDistribuicaoSolicitacao solicitacao, CancellationToken cancellationToken = default);
}
