using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OpenAC.Net.NFe.Common.Model;
using OpenAC.Net.NFe.Common.Types;
using OpenAC.Net.NFe.DANFe.PDFSharp.Common;
using OpenAC.Net.NFe.DANFe.PDFSharp.Configuracao;
using OpenAC.Net.NFe.DANFe.PDFSharp.Layout;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using NotaFiscal = OpenAC.Net.NFe.Common.Model.NFe;

namespace OpenAC.Net.NFe.DANFe.PDFSharp;

/// <summary>Gera DANFE A4, NFC-e, eventos e inutilização em PDF, sem estado de emissão ou impressora.</summary>
public class OpenDANFe
{
    public OpenDANFe(DANFeConfig configuracao = null) => Configuracoes = configuracao?.Copiar() ?? new DANFeConfig();

    public DANFeConfig Configuracoes { get; }

    public byte[] GerarPDF(NotaFiscal nota) => GerarPDF(NFeImpressao.ParaImpressao(nota));
    public byte[] GerarPDF(params NFeProc[] notas) => GerarPDF(CancellationToken.None, notas);
    public byte[] GerarPDF(CancellationToken cancellationToken, params NFeProc[] notas)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ValidarColecao(notas, nameof(notas));
        var configuracao = ConfigurarOperacao();
        using var documento = CriarDocumento("DANFE NF-e / NFC-e", configuracao);
        foreach (var nota in notas)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (nota.NFe?.InfNFe?.Ide == null || nota.NFe.InfNFe.Emit == null || nota.NFe.InfNFe.Total?.ICMSTot == null)
                throw new ArgumentException("Informe identificação, emitente e totais da NF-e.", nameof(notas));
            if (nota.Modelo is not (ModeloNFe.NFe or ModeloNFe.NFCe)) throw new ArgumentException("Modelo deve ser 55 ou 65.", nameof(notas));
            if (nota.ProtNFe != null) nota.ValidarProtocolo();
            RenderizarNota(documento, nota, configuracao, cancellationToken);
        }
        return Salvar(documento, configuracao, cancellationToken);
    }

    public byte[] GerarEventoPDF(NFeEvento evento) => GerarEventoPDF(new NFeProcEvento { Evento = evento });
    public byte[] GerarEventoPDF(params NFeProcEvento[] eventos) => GerarEventoPDF(CancellationToken.None, eventos);
    public byte[] GerarEventoPDF(CancellationToken cancellationToken, params NFeProcEvento[] eventos)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ValidarColecao(eventos, nameof(eventos));
        var configuracao = ConfigurarOperacao();
        using var documento = CriarDocumento("Eventos NF-e / NFC-e", configuracao);
        foreach (var evento in eventos)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (evento.Evento?.InfEvento == null) throw new ArgumentException("Informações do evento não informadas.", nameof(eventos));
            RenderizarEvento(documento, evento, configuracao, cancellationToken);
        }
        return Salvar(documento, configuracao, cancellationToken);
    }

    public byte[] GerarInutilizacaoPDF(NFeProcInutilizacao inutilizacao, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (inutilizacao?.Inutilizacao?.InfInut == null) throw new ArgumentException("Solicitação de inutilização não informada.", nameof(inutilizacao));
        var configuracao = ConfigurarOperacao();
        using var documento = CriarDocumento("Inutilização NF-e / NFC-e", configuracao);
        RenderizarInutilizacao(documento, inutilizacao, configuracao, cancellationToken);
        return Salvar(documento, configuracao, cancellationToken);
    }

    public void GerarPDF(Stream destino, NotaFiscal nota) => Gravar(destino, GerarPDF(nota));
    public void GerarPDF(Stream destino, params NFeProc[] notas) => Gravar(destino, GerarPDF(notas));
    public void GerarPDF(string arquivo, NotaFiscal nota) => File.WriteAllBytes(arquivo, GerarPDF(nota));
    public void GerarPDF(string arquivo, params NFeProc[] notas) => File.WriteAllBytes(arquivo, GerarPDF(notas));
    public void GerarEventoPDF(Stream destino, params NFeProcEvento[] eventos) => Gravar(destino, GerarEventoPDF(eventos));
    public void GerarEventoPDF(string arquivo, params NFeProcEvento[] eventos) => File.WriteAllBytes(arquivo, GerarEventoPDF(eventos));
    public void GerarInutilizacaoPDF(Stream destino, NFeProcInutilizacao inutilizacao) => Gravar(destino, GerarInutilizacaoPDF(inutilizacao));
    public void GerarInutilizacaoPDF(string arquivo, NFeProcInutilizacao inutilizacao) => File.WriteAllBytes(arquivo, GerarInutilizacaoPDF(inutilizacao));

    public async Task GravarPDFAsync(string arquivo, IEnumerable<NFeProc> notas, CancellationToken cancellationToken = default)
    {
        var bytes = GerarPDF(cancellationToken, notas?.ToArray() ?? throw new ArgumentNullException(nameof(notas)));
        using var stream = new FileStream(arquivo, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true);
        await stream.WriteAsync(bytes, 0, bytes.Length, cancellationToken).ConfigureAwait(false);
    }

    protected virtual void RenderizarNota(PdfDocument documento, NFeProc nota, DANFeConfig configuracao, CancellationToken cancellationToken)
    {
        if (nota.Modelo == ModeloNFe.NFCe) DANFeNFCeLayout.Renderizar(documento, nota, configuracao, cancellationToken);
        else DANFeA4Layout.Renderizar(documento, nota, configuracao, cancellationToken);
    }
    protected virtual void RenderizarEvento(PdfDocument documento, NFeProcEvento evento, DANFeConfig configuracao, CancellationToken cancellationToken) =>
        DANFeEventoLayout.Renderizar(documento, evento, configuracao, cancellationToken);
    protected virtual void RenderizarInutilizacao(PdfDocument documento, NFeProcInutilizacao inutilizacao, DANFeConfig configuracao, CancellationToken cancellationToken) =>
        DANFeInutilizacaoLayout.Renderizar(documento, inutilizacao, configuracao, cancellationToken);

    private DANFeConfig ConfigurarOperacao()
    {
        var configuracao = Configuracoes.Copiar();
        configuracao.Validar();
        DANFeFontResolver.Initialize();
        return configuracao;
    }
    private static PdfDocument CriarDocumento(string titulo, DANFeConfig configuracao)
    {
        var documento = new PdfDocument();
        documento.Info.Title = titulo;
        documento.Info.Creator = "OpenAC.Net.NFe.DANFe.PDFSharp";
        if (!string.IsNullOrEmpty(configuracao.SenhaUsuario) || !string.IsNullOrEmpty(configuracao.SenhaProprietario))
        {
            documento.SecuritySettings.UserPassword = configuracao.SenhaUsuario ?? "";
            documento.SecuritySettings.OwnerPassword = configuracao.SenhaProprietario ?? Guid.NewGuid().ToString("N");
            documento.SecuritySettings.PermitPrint = configuracao.PermitirImpressao;
            documento.SecuritySettings.PermitExtractContent = configuracao.PermitirCopia;
            documento.SecuritySettings.PermitModifyDocument = false;
        }
        return documento;
    }
    private static byte[] Salvar(PdfDocument documento, DANFeConfig configuracao, CancellationToken cancellationToken)
    {
        var fonte = new XFont("Liberation Sans", 6);
        for (var indice = 0; indice < documento.PageCount; indice++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var pagina = documento.Pages[indice];
            using var grafico = XGraphics.FromPdfPage(pagina, XGraphicsPdfPageOptions.Append);
            var mensagem = configuracao.MensagemRodape ?? "";
            var paginaTexto = $"Página {indice + 1}/{documento.PageCount}";
            var texto = mensagem.Length > 0 ? mensagem + " | " + paginaTexto : paginaTexto;
            grafico.DrawString(texto, fonte, XBrushes.Black, new XRect(8, pagina.Height.Point - 12, pagina.Width.Point - 16, 9), XStringFormats.TopCenter);
        }
        using var stream = new MemoryStream();
        documento.Save(stream, false);
        return stream.ToArray();
    }
    private static void Gravar(Stream destino, byte[] bytes)
    {
        if (destino is not { CanWrite: true }) throw new ArgumentException("Stream de destino deve permitir escrita.", nameof(destino));
        destino.Write(bytes, 0, bytes.Length);
    }
    private static void ValidarColecao<T>(T[] documentos, string parametro)
    {
        if (documentos == null || documentos.Length == 0 || documentos.Any(item => item == null))
            throw new ArgumentException("Informe pelo menos um documento válido.", parametro);
    }
}
