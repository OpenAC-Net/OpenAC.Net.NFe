using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using OpenAC.Net.EscPos;
using OpenAC.Net.EscPos.Commom;
using OpenAC.Net.NFe.Common.Model;
using OpenAC.Net.NFe.DANFe.EscPos.Common;
using OpenAC.Net.NFe.DANFe.EscPos.Configuracao;
using OpenAC.Net.NFe.DANFe.EscPos.Layout;
using NotaFiscal = OpenAC.Net.NFe.Common.Model.NFe;

namespace OpenAC.Net.NFe.DANFe.EscPos;

/// <summary>Compõe e imprime NFC-e usando OpenAC.Net.EscPos para comandos, codificação e dispositivo.</summary>
public class OpenDANFeEscPos
{
    private static readonly ConditionalWeakTable<EscPosPrinter, SemaphoreSlim> Filas = new();

    public OpenDANFeEscPos(DANFeEscPosConfig configuracao = null) =>
        Configuracoes = configuracao?.Copiar() ?? new DANFeEscPosConfig();

    public DANFeEscPosConfig Configuracoes { get; }

    public string GerarTexto(NotaFiscal nota, CancellationToken cancellationToken = default) =>
        GerarTexto(NFeImpressao.ParaImpressao(nota), cancellationToken);

    public string GerarTexto(NFeProc nota, CancellationToken cancellationToken = default)
    {
        var configuracao = Configuracoes.Copiar();
        return NFCeEscPosCupom.Criar(nota, configuracao, configuracao.Colunas ?? 48, cancellationToken).GerarTexto();
    }

    /// <summary>Gera bytes pelo componente com um dispositivo de saída em memória.</summary>
    public byte[] Gerar(NotaFiscal nota, ProtocoloEscPos protocolo = ProtocoloEscPos.EscPos,
        PaginaCodigo paginaCodigo = PaginaCodigo.pc850, CancellationToken cancellationToken = default) =>
        Gerar(NFeImpressao.ParaImpressao(nota), protocolo, paginaCodigo, cancellationToken);

    public byte[] Gerar(NFeProc nota, ProtocoloEscPos protocolo = ProtocoloEscPos.EscPos,
        PaginaCodigo paginaCodigo = PaginaCodigo.pc850, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var configuracao = Configuracoes.Copiar();
        using var saida = new MemoryStream();
        using var impressora = CriarImpressora(saida, configuracao, protocolo, paginaCodigo);
        RenderizarCupom(nota, impressora, configuracao, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        impressora.Conectar();
        impressora.Imprimir(configuracao.Copias);
        return saida.ToArray();
    }

    /// <summary>Gera a prévia PNG pelo componente, sem conectar uma impressora.</summary>
    public byte[] GerarPreview(NotaFiscal nota, CancellationToken cancellationToken = default) =>
        GerarPreview(NFeImpressao.ParaImpressao(nota), cancellationToken);

    public byte[] GerarPreview(NFeProc nota, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var configuracao = Configuracoes.Copiar();
        using var saida = new MemoryStream();
        using var impressora = CriarImpressora(saida, configuracao, ProtocoloEscPos.EscPos, PaginaCodigo.pc850);
        RenderizarCupom(nota, impressora, configuracao, cancellationToken);
        var imagem = impressora.GerarPreview();
        cancellationToken.ThrowIfCancellationRequested();
        return imagem;
    }

    /// <summary>Adiciona o cupom ao buffer para composição ou prévia, sem enviar ao dispositivo.</summary>
    public void PrepararCupom(NotaFiscal nota, EscPosPrinter impressora,
        CancellationToken cancellationToken = default) =>
        PrepararCupom(NFeImpressao.ParaImpressao(nota), impressora, cancellationToken);

    public void PrepararCupom(NFeProc nota, EscPosPrinter impressora, CancellationToken cancellationToken = default)
    {
        if (impressora == null) throw new ArgumentNullException(nameof(impressora));
        cancellationToken.ThrowIfCancellationRequested();
        var configuracao = Configuracoes.Copiar();
        var fila = Fila(impressora);
        fila.Wait(cancellationToken);
        try
        {
            RenderizarCupom(nota, impressora, configuracao, cancellationToken);
        }
        finally
        {
            fila.Release();
        }
    }

    /// <summary>Imprime preservando a conexão configurada pelo chamador.</summary>
    public void Imprimir(NotaFiscal nota, EscPosPrinter impressora, CancellationToken cancellationToken = default) =>
        Imprimir(NFeImpressao.ParaImpressao(nota), impressora, cancellationToken);

    public void Imprimir(NFeProc nota, EscPosPrinter impressora, CancellationToken cancellationToken = default)
    {
        ValidarImpressora(impressora, cancellationToken);
        var configuracao = Configuracoes.Copiar();
        var fila = Fila(impressora);
        fila.Wait(cancellationToken);
        try
        {
            Enviar(nota, impressora, configuracao, cancellationToken);
        }
        finally
        {
            fila.Release();
        }
    }

    public Task ImprimirAsync(NotaFiscal nota, EscPosPrinter impressora,
        CancellationToken cancellationToken = default) =>
        ImprimirAsync(NFeImpressao.ParaImpressao(nota), impressora, cancellationToken);

    /// <summary>Aguarda a vez da impressora com cancelamento; o envio usa a API síncrona do componente.</summary>
    public async Task ImprimirAsync(NFeProc nota, EscPosPrinter impressora,
        CancellationToken cancellationToken = default)
    {
        ValidarImpressora(impressora, cancellationToken);
        var configuracao = Configuracoes.Copiar();
        var fila = Fila(impressora);
        await fila.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            Enviar(nota, impressora, configuracao, cancellationToken);
        }
        finally
        {
            fila.Release();
        }
    }

    protected virtual void RenderizarCupom(NFeProc nota, EscPosPrinter impressora, DANFeEscPosConfig configuracao,
        CancellationToken cancellationToken)
    {
        var cupom = NFCeEscPosCupom.Criar(nota, configuracao, configuracao.Colunas ?? impressora.Colunas,
            cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        cupom.Aplicar(impressora, configuracao);
    }

    private void Enviar(NFeProc nota, EscPosPrinter impressora, DANFeEscPosConfig configuracao,
        CancellationToken cancellationToken)
    {
        ValidarImpressora(impressora, cancellationToken);
        RenderizarCupom(nota, impressora, configuracao, cancellationToken);
        // Não interrompe nem repete uma escrita que já iniciou no dispositivo.
        impressora.Imprimir(configuracao.Copias);
    }

    private static EscPosPrinter<CupomMemoriaConfig> CriarImpressora(MemoryStream saida, DANFeEscPosConfig configuracao,
        ProtocoloEscPos protocolo, PaginaCodigo paginaCodigo) => new(new CupomMemoriaConfig(saida))
        { Protocolo = protocolo, PaginaCodigo = paginaCodigo, Colunas = configuracao.Colunas ?? 48 };

    private static SemaphoreSlim Fila(EscPosPrinter impressora) =>
        Filas.GetValue(impressora, _ => new SemaphoreSlim(1, 1));

    private static void ValidarImpressora(EscPosPrinter impressora, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (impressora == null) throw new ArgumentNullException(nameof(impressora));
        if (!impressora.Conectado)
            throw new InvalidOperationException("Conecte o componente ESC/POS antes de imprimir.");
    }
}