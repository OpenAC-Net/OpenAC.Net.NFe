using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using OpenAC.Net.NFe.DANFe.PDFSharp.Common;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using QRCoder;

namespace OpenAC.Net.NFe.DANFe.PDFSharp.Layout;

/// <summary>Desenho e paginação comuns aos layouts PDF.</summary>
internal sealed class DANFeReport : IDisposable
{
    private readonly PdfDocument documento;
    private readonly double largura, altura, margem;
    private readonly CancellationToken cancelamento;
    private readonly Action<DANFeReport> cabecalho;
    private readonly IReadOnlyList<double> alturas;
    private readonly int primeiraPagina;
    private XGraphics grafico;
    private readonly XFont normal = new("Liberation Sans", 8);
    private readonly XFont negrito = new("Liberation Sans", 8, XFontStyleEx.Bold);
    private readonly XFont pequeno = new("Liberation Sans", 5.5);
    private readonly List<double> alturasMedidas = new();
    private double inicioConteudo;
    private bool encerrado;
    private bool desenhandoCabecalho;
    internal double Y { get; private set; }
    internal double LarguraUtil => largura - margem * 2;
    internal bool PrimeiraPagina => documento.PageCount == primeiraPagina + 1;
    internal IReadOnlyList<double> AlturasMedidas => alturasMedidas;
    private double AlturaPagina => alturas == null ? altura : alturas[Math.Min(documento.PageCount - primeiraPagina - 1, alturas.Count - 1)];

    internal DANFeReport(PdfDocument documento, double largura, double altura, double margem,
        CancellationToken cancelamento, Action<DANFeReport> cabecalho = null, IReadOnlyList<double> alturas = null)
    {
        this.documento = documento; this.largura = largura; this.altura = altura; this.margem = margem;
        this.cancelamento = cancelamento; this.cabecalho = cabecalho; this.alturas = alturas;
        primeiraPagina = documento.PageCount;
        NovaPagina();
    }

    private void NovaPagina()
    {
        cancelamento.ThrowIfCancellationRequested();
        if (grafico != null) { alturasMedidas.Add(Math.Min(14400, Math.Max(120, Y + margem + 20))); grafico.Dispose(); }
        var pagina = documento.AddPage();
        pagina.Width = XUnit.FromPoint(largura);
        pagina.Height = XUnit.FromPoint(AlturaPagina);
        grafico = XGraphics.FromPdfPage(pagina);
        Y = margem;
        desenhandoCabecalho = true;
        cabecalho?.Invoke(this);
        desenhandoCabecalho = false;
        inicioConteudo = Y;
    }

    internal bool Reservar(double alturaBloco)
    {
        cancelamento.ThrowIfCancellationRequested();
        if (desenhandoCabecalho) return false;
        if (Y + alturaBloco <= AlturaPagina - margem - 16 || Y <= inicioConteudo + 0.01) return false;
        NovaPagina(); return true;
    }

    internal IEnumerable<string> Quebrar(string texto, double espaco, XFont fonte = null)
    {
        fonte ??= normal;
        foreach (var paragrafo in (texto ?? "").Replace("\r", "").Split('\n'))
        {
            var linha = "";
            foreach (var palavra in paragrafo.Split(' '))
            {
                var candidata = linha.Length == 0 ? palavra : linha + " " + palavra;
                if (grafico.MeasureString(candidata, fonte).Width <= espaco) { linha = candidata; continue; }
                if (linha.Length > 0) { yield return linha; linha = ""; }
                var restante = palavra;
                while (restante.Length > 1 && grafico.MeasureString(restante, fonte).Width > espaco)
                {
                    var tamanho = restante.Length - 1;
                    while (tamanho > 1 && grafico.MeasureString(restante.Substring(0, tamanho), fonte).Width > espaco) tamanho--;
                    yield return restante.Substring(0, tamanho); restante = restante.Substring(tamanho);
                }
                linha = restante;
            }
            yield return linha;
        }
    }

    internal void Texto(string texto, bool forte = false, bool centralizar = false)
    {
        var fonte = forte ? negrito : normal;
        foreach (var linha in Quebrar(texto, LarguraUtil, fonte))
        {
            Reservar(11);
            grafico.DrawString(linha, fonte, XBrushes.Black, new XRect(margem, Y, LarguraUtil, 11),
                centralizar ? XStringFormats.TopCenter : XStringFormats.TopLeft);
            Y += 11;
        }
    }

    internal void Separador()
    {
        Reservar(8); Y += 3; grafico.DrawLine(XPens.Black, margem, Y, margem + LarguraUtil, Y); Y += 5;
    }
    internal void Titulo(string texto) { Reservar(24); Y += 3; Texto(texto, true); }
    internal void Secao(string titulo, IEnumerable<string> linhas)
    {
        var conteudo = linhas.Where(linha => !string.IsNullOrWhiteSpace(linha)).ToArray();
        if (conteudo.Length == 0) return;
        Titulo(titulo);
        foreach (var linha in conteudo) Texto(linha);
        Y += 3;
    }
    internal void Par(string rotulo, string valor, bool forte = false)
    {
        var fonte = forte ? negrito : normal;
        var esquerda = Quebrar(rotulo, LarguraUtil * .64, fonte).ToArray();
        var direita = Quebrar(valor, LarguraUtil * .35, fonte).ToArray();
        var quantidade = Math.Max(esquerda.Length, direita.Length);
        for (var i = 0; i < quantidade; i++)
        {
            Reservar(11);
            grafico.DrawString(i < esquerda.Length ? esquerda[i] : "", fonte, XBrushes.Black,
                new XRect(margem, Y, LarguraUtil * .64, 11), XStringFormats.TopLeft);
            grafico.DrawString(i < direita.Length ? direita[i] : "", fonte, XBrushes.Black,
                new XRect(margem + LarguraUtil * .65, Y, LarguraUtil * .35, 11), XStringFormats.TopRight);
            Y += 11;
        }
    }
    internal void Quadros(params (string Rotulo, string Valor, double Peso)[] campos)
    {
        var soma = campos.Sum(campo => campo.Peso);
        var rotulos = campos.Select(campo => Quebrar(campo.Rotulo, LarguraUtil * campo.Peso / soma - 6, pequeno).ToArray()).ToArray();
        var valores = campos.Select(campo => Quebrar(campo.Valor, LarguraUtil * campo.Peso / soma - 6).ToArray()).ToArray();
        var alturaRotulo = rotulos.Max(linha => linha.Length) * 7d;
        var alturaBloco = alturaRotulo + valores.Max(linha => linha.Length) * 10 + 6;
        if (alturaBloco > altura - margem * 2 - 120)
        {
            foreach (var campo in campos) Secao(campo.Rotulo, new[] { campo.Valor });
            return;
        }
        Reservar(alturaBloco);
        var x = margem;
        for (var i = 0; i < campos.Length; i++)
        {
            var w = LarguraUtil * campos[i].Peso / soma;
            grafico.DrawRectangle(XPens.Black, x, Y, w, alturaBloco);
            for (var j = 0; j < rotulos[i].Length; j++) grafico.DrawString(rotulos[i][j], pequeno, XBrushes.Black, new XPoint(x + 3, Y + 7 + j * 7));
            for (var j = 0; j < valores[i].Length; j++) grafico.DrawString(valores[i][j], normal, XBrushes.Black, new XPoint(x + 3, Y + alturaRotulo + 11 + j * 10));
            x += w;
        }
        Y += alturaBloco;
    }

    internal void Identificacao(string emitente, string endereco, string documentoEmitente, string numero,
        string serie, string entradaSaida, string chave, byte[] logo)
    {
        var esquerda = LarguraUtil * .39; var centro = LarguraUtil * .17; var direita = LarguraUtil - esquerda - centro;
        var linhas = Quebrar(emitente + "\n" + endereco + "\n" + documentoEmitente, esquerda - 10).ToArray();
        var alturaLogo = logo?.Length > 0 ? 36 : 0;
        var h = Math.Max(100, linhas.Length * 10 + alturaLogo + 10);
        Reservar(h);
        var x = margem;
        grafico.DrawRectangle(XPens.Black, x, Y, esquerda, h);
        if (alturaLogo > 0) Imagem(logo, x + 5, Y + 4, esquerda - 10, 32);
        for (var i = 0; i < linhas.Length; i++) grafico.DrawString(linhas[i], i == 0 ? negrito : normal,
            XBrushes.Black, new XPoint(x + 5, Y + alturaLogo + 13 + i * 10));
        x += esquerda;
        grafico.DrawRectangle(XPens.Black, x, Y, centro, h);
        var titulo = new XFont("Liberation Sans", 15, XFontStyleEx.Bold);
        grafico.DrawString("DANFE", titulo, XBrushes.Black, new XRect(x, Y + 5, centro, 20), XStringFormats.TopCenter);
        var textos = new[] { "Documento Auxiliar da", "Nota Fiscal Eletrônica", "0 - ENTRADA / 1 - SAÍDA", entradaSaida, "Nº " + numero, "SÉRIE " + serie };
        for (var i = 0; i < textos.Length; i++) grafico.DrawString(textos[i] ?? "", i < 3 ? pequeno : negrito,
            XBrushes.Black, new XRect(x + 2, Y + 28 + i * 11, centro - 4, 11), XStringFormats.TopCenter);
        x += centro;
        grafico.DrawRectangle(XPens.Black, x, Y, direita, h);
        if (chave?.Length == 44) Code128.Draw(grafico, chave, x + 10, Y + 8, direita - 20, 31);
        grafico.DrawString("CHAVE DE ACESSO", pequeno, XBrushes.Black, new XPoint(x + 5, Y + 51));
        var chaveFormatada = NFeImpressao.Chave(chave);
        grafico.DrawString(chaveFormatada, new XFont("Liberation Sans", 6.5), XBrushes.Black,
            new XRect(x + 3, Y + 56, direita - 6, 12), XStringFormats.TopCenter);
        grafico.DrawString("Consulta de autenticidade no portal nacional da NF-e", pequeno, XBrushes.Black,
            new XRect(x + 3, Y + 76, direita - 6, 9), XStringFormats.TopCenter);
        grafico.DrawString("www.nfe.fazenda.gov.br/portal ou no site da SEFAZ autorizadora", pequeno, XBrushes.Black,
            new XRect(x + 3, Y + 86, direita - 6, 9), XStringFormats.TopCenter);
        Y += h;
    }

    private void Imagem(byte[] bytes, double x, double y, double w, double h)
    {
        using var stream = new MemoryStream(bytes, false);
        using var imagem = XImage.FromStream(stream);
        var escala = Math.Min(w / imagem.PointWidth, h / imagem.PointHeight);
        var larguraLogo = imagem.PointWidth * escala; var alturaLogo = imagem.PointHeight * escala;
        grafico.DrawImage(imagem, x + (w - larguraLogo) / 2, y, larguraLogo, alturaLogo);
    }
    internal void Logo(byte[] bytes)
    {
        if (bytes is not { Length: > 0 }) return;
        Reservar(43); Imagem(bytes, margem, Y, LarguraUtil, 36); Y += 41;
    }
    internal void CodigoBarras(string chave)
    {
        if (chave?.Length != 44) return;
        Reservar(42); Code128.Draw(grafico, chave, margem + 5, Y + 2, LarguraUtil - 10, 25); Y += 30; Texto(NFeImpressao.Chave(chave), centralizar: true);
    }
    internal void QrCode(string valor)
    {
        if (string.IsNullOrWhiteSpace(valor)) return;
        var tamanho = Math.Min(145, LarguraUtil - 6); Reservar(tamanho + 6);
        using var gerador = new QRCodeGenerator();
        using var dados = gerador.CreateQrCode(valor, QRCodeGenerator.ECCLevel.M);
        var modulo = tamanho / dados.ModuleMatrix.Count;
        var x = margem + (LarguraUtil - tamanho) / 2;
        for (var linha = 0; linha < dados.ModuleMatrix.Count; linha++)
            for (var coluna = 0; coluna < dados.ModuleMatrix.Count; coluna++)
                if (dados.ModuleMatrix[linha][coluna]) grafico.DrawRectangle(XBrushes.Black, x + coluna * modulo,
                    Y + linha * modulo, modulo, modulo);
        Y += tamanho + 6;
    }

    internal void Tabela(string[] titulos, double[] pesos, IEnumerable<string[]> registros, double reservaFinal = 0)
    {
        var soma = pesos.Sum();
        string[][] Linhas(string[] celulas) => celulas.Select((valor, i) => Quebrar(valor, LarguraUtil * pesos[i] / soma - 5, pequeno).ToArray()).ToArray();
        void Desenhar(string[][] linhas, int inicio, int quantidade, bool cabecalhoTabela)
        {
            var h = Math.Max(14, quantidade * 8 + 6); var x = margem;
            for (var coluna = 0; coluna < pesos.Length; coluna++)
            {
                var w = LarguraUtil * pesos[coluna] / soma;
                grafico.DrawRectangle(XPens.Black, x, Y, w, h);
                for (var linha = 0; linha < quantidade && inicio + linha < linhas[coluna].Length; linha++)
                    grafico.DrawString(linhas[coluna][inicio + linha], pequeno, XBrushes.Black, new XPoint(x + 2, Y + 9 + linha * 8));
                x += w;
            }
            Y += h;
        }
        var rotulos = Linhas(titulos); var alturaCabecalho = rotulos.Max(linhas => linhas.Length);
        Reservar(alturaCabecalho * 8 + 25); Desenhar(rotulos, 0, alturaCabecalho, true);
        foreach (var registro in registros)
        {
            cancelamento.ThrowIfCancellationRequested();
            var linhas = Linhas(registro); var quantidade = linhas.Max(campo => campo.Length); var inicio = 0;
            while (inicio < quantidade)
            {
                if (Y + 15 > AlturaPagina - margem - 16)
                { NovaPagina(); Desenhar(rotulos, 0, alturaCabecalho, true); }
                var cabem = (int)Math.Floor((AlturaPagina - margem - 16 - Y - 6) / 8);
                if (cabem < 1) { NovaPagina(); Desenhar(rotulos, 0, alturaCabecalho, true); continue; }
                var bloco = Math.Min(quantidade - inicio, cabem);
                Desenhar(linhas, inicio, bloco, false); inicio += bloco;
            }
        }
        if (reservaFinal > 0)
        {
            var restante = AlturaPagina - margem - 16 - reservaFinal - Y;
            if (restante > 4)
            {
                var x = margem;
                foreach (var peso in pesos)
                {
                    var w = LarguraUtil * peso / soma;
                    grafico.DrawRectangle(XPens.Black, x, Y, w, restante);
                    x += w;
                }
                Y += restante;
            }
        }
        Y += 4;
    }

    public void Dispose()
    {
        if (encerrado) return;
        alturasMedidas.Add(Math.Min(14400, Math.Max(120, Y + margem + 20)));
        grafico?.Dispose(); grafico = null; encerrado = true;
    }
}
