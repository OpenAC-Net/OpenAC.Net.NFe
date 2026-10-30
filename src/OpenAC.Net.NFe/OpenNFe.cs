using System;
using System.Threading;
using System.Threading.Tasks;
using OpenAC.Net.NFe.Certificates;
using OpenAC.Net.NFe.Common;
using OpenAC.Net.NFe.Common.Model;

namespace OpenAC.Net.NFe;

/// <summary>Cliente reutilizável. Configure uma instância por fluxo de operações, como no CT-e.</summary>
public sealed class OpenNFe : IOpenNFeClient
{
    private readonly INFeCertificadoProvider certificados;

    public OpenNFe() : this(new NFeConfig()) { }

    public OpenNFe(NFeConfig configuracao, INFeCertificadoProvider certificados = null)
    {
        Configuracoes = configuracao ?? throw new ArgumentNullException(nameof(configuracao));
        this.certificados = certificados;
    }

    /// <summary>Alterações são utilizadas pelas próximas operações. Não altere durante uma operação em andamento.</summary>
    public NFeConfig Configuracoes { get; }

    private NFeOperationClient CriarOperacao(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return new NFeOperationClient(Configuracoes, certificados);
    }

    public async Task<NFeAutorizacaoResposta> EnviarAsync(NFeEnvioSolicitacao solicitacao, CancellationToken cancellationToken = default)
        => await CriarOperacao(cancellationToken).EnviarAsync(solicitacao, cancellationToken).ConfigureAwait(false);

    public async Task<NFeConsultaResposta> ConsultarAsync(string chave, CancellationToken cancellationToken = default)
        => await CriarOperacao(cancellationToken).ConsultarAsync(chave, cancellationToken).ConfigureAwait(false);

    public async Task<NFeStatusServicoResposta> ConsultarSituacaoServicoAsync(CancellationToken cancellationToken = default)
        => await CriarOperacao(cancellationToken).ConsultarSituacaoServicoAsync(cancellationToken).ConfigureAwait(false);

    public async Task<NFeReciboResposta> ConsultaReciboAsync(NFeReciboSolicitacao solicitacao, CancellationToken cancellationToken = default)
        => await CriarOperacao(cancellationToken).ConsultaReciboAsync(solicitacao, cancellationToken).ConfigureAwait(false);

    public async Task<NFeEventoResposta> EnviarEventoAsync(NFeEventoSolicitacao solicitacao, CancellationToken cancellationToken = default)
        => await CriarOperacao(cancellationToken).EnviarEventoAsync(solicitacao, cancellationToken).ConfigureAwait(false);

    public async Task<NFeCadastroResposta> ConsultaCadastroAsync(string documento, string tipo = "CNPJ", CancellationToken cancellationToken = default)
        => await CriarOperacao(cancellationToken).ConsultaCadastroAsync(documento, tipo, cancellationToken).ConfigureAwait(false);

    public async Task<NFeInutilizacaoResposta> InutilizarAsync(NFeInutilizacaoSolicitacao solicitacao, CancellationToken cancellationToken = default)
        => await CriarOperacao(cancellationToken).InutilizarAsync(solicitacao, cancellationToken).ConfigureAwait(false);

    public async Task<NFeDistribuicaoResposta> DistribuicaoDFeAsync(NFeDistribuicaoSolicitacao solicitacao, CancellationToken cancellationToken = default)
        => await CriarOperacao(cancellationToken).DistribuicaoDFeAsync(solicitacao, cancellationToken).ConfigureAwait(false);
}
