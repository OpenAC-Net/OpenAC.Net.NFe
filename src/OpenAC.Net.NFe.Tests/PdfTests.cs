using System.Text;
using Microsoft.Extensions.DependencyInjection;
using OpenAC.Net.NFe.Common.Model;
using OpenAC.Net.NFe.DANFe.PDFSharp;
using OpenAC.Net.NFe.DANFe.PDFSharp.Configuracao;
using OpenAC.Net.NFe.DANFe.PDFSharp.Web;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using Xunit;

namespace OpenAC.Net.NFe.Tests;

public sealed class PdfTests
{
    [Theory]
    [InlineData(false, 210, 297)]
    [InlineData(true, 297, 210)]
    public void A4RespeitaOrientacaoEUsaMetadadosDaBiblioteca(bool paisagem, int largura, int altura)
    {
        var gerador = new OpenDANFe(new DANFeConfig { Paisagem = paisagem });
        var bytes = gerador.GerarPDF(Fixtures.Nota());
        using var pdf = Abrir(bytes);
        Assert.Single(pdf.Pages.Cast<PdfPage>());
        Assert.InRange(pdf.Pages[0].Width.Millimeter, largura - .01, largura + .01);
        Assert.InRange(pdf.Pages[0].Height.Millimeter, altura - .01, altura + .01);
        Assert.Equal("OpenAC.Net.NFe.DANFe.PDFSharp", pdf.Info.Creator);
    }

    [Theory]
    [InlineData(58)]
    [InlineData(80)]
    public void CupomAjustaAlturaAoConteudoEProduzArtefato(int largura)
    {
        using var certificado = Fixtures.Certificate();
        var nota = Fixtures.Nota(65, itens: 20);
        nota.Assinar(certificado);
        nota.GerarQrCode(Fixtures.Config(certificado, 65));
        var original = nota.GetXml();
        var gerador = new OpenDANFe(new DANFeConfig { LarguraNFCeMilimetros = largura });
        var bytes = gerador.GerarPDF(nota.ComProtocolo(Fixtures.Protocolo(nota)));
        using var pdf = Abrir(bytes);
        Assert.Single(pdf.Pages.Cast<PdfPage>());
        Assert.InRange(pdf.Pages[0].Width.Millimeter, largura - .01, largura + .01);
        Assert.InRange(pdf.Pages[0].Height.Point, 400, 5000);
        Assert.Equal(original, nota.GetXml());
        Assert.True(nota.ValidarAssinatura());
        Artefato($"nfce-modernizada-{largura}.pdf", bytes);
    }

    [Fact]
    public void DescricaoMuitoLongaContinuaEmOutrasPaginas()
    {
        var nota = Fixtures.Nota();
        nota.InfNFe.Det[0].Prod.XProd = string.Join(" ", Enumerable.Repeat("CONTINUACAO DO PRODUTO", 1200)) + " FIM-DO-PRODUTO";
        nota.InfNFe.InfAdic.InfCpl = "OBSERVACAO-APOS-O-PRODUTO";
        var bytes = new OpenDANFe().GerarPDF(nota);
        using var pdf = Abrir(bytes);
        Assert.True(pdf.PageCount > 3);
        Assert.All(pdf.Pages.Cast<PdfPage>(), pagina => Assert.InRange(pagina.Height.Millimeter, 296.99, 297.01));
        Artefato("danfe-produto-longo.pdf", bytes);
    }

    [Fact]
    public void CupomExtensoContinuaDentroDoLimiteDePaginaDoPdf()
    {
        var gerador = new OpenDANFe(new DANFeConfig { LarguraNFCeMilimetros = 58 });
        var bytes = gerador.GerarPDF(Fixtures.Nota(65, itens: 600));
        using var pdf = Abrir(bytes);
        Assert.True(pdf.PageCount > 1);
        Assert.All(pdf.Pages.Cast<PdfPage>(), pagina =>
        {
            Assert.InRange(pagina.Width.Millimeter, 57.99, 58.01);
            Assert.InRange(pagina.Height.Point, 120, 14400);
        });
        Artefato("nfce-cupom-extenso.pdf", bytes);
    }

    [Fact]
    public void LogoEmMemoriaFuncionaSemArquivoOuInterfaceGrafica()
    {
        var configuracao = new DANFeConfig
        {
            Logo = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAABAAAAAICAIAAAB/FOjAAAAAE0lEQVR4nGNQCJhAEmIY1UALDQDPN4ABfbuYvwAAAABJRU5ErkJggg=="),
            MensagemRodape = "EMPRESA DE TESTE"
        };
        var bytes = new OpenDANFe(configuracao).GerarPDF(Fixtures.Nota());
        using var pdf = Abrir(bytes);
        Assert.True(pdf.Pages[0].Resources.Elements.ContainsKey("/XObject"));
        Artefato("danfe-com-logo.pdf", bytes);
    }

    [Fact]
    public void LotePodeConterNFeENFCeComPapeisDiferentes()
    {
        var primeira = Fixtures.Nota();
        var segunda = Fixtures.Nota(65);
        var bytes = new OpenDANFe().GerarPDF(new NFeProc { NFe = primeira }, new NFeProc { NFe = segunda });
        using var pdf = Abrir(bytes);
        Assert.Equal(2, pdf.PageCount);
        Assert.InRange(pdf.Pages[0].Width.Millimeter, 209.99, 210.01);
        Assert.InRange(pdf.Pages[1].Width.Millimeter, 79.99, 80.01);
    }

    [Fact]
    public void ConfiguracaoEClonadaPorOperacao()
    {
        var configuracao = new DANFeConfig { LarguraNFCeMilimetros = 80, Logo = [1, 2, 3] };
        var copia = configuracao.Copiar();
        configuracao.Logo[0] = 9;
        Assert.Equal(1, copia.Logo[0]);
        var gerador = new GeradorQueAlteraConfiguracao();
        var nota = Fixtures.Nota(65);
        var bytes = gerador.GerarPDF(new NFeProc { NFe = nota }, new NFeProc { NFe = nota });
        using var pdf = Abrir(bytes);
        Assert.All(pdf.Pages.Cast<PdfPage>(), pagina => Assert.InRange(pagina.Width.Millimeter, 79.99, 80.01));
        using var proximo = Abrir(gerador.GerarPDF(nota));
        Assert.InRange(proximo.Pages[0].Width.Millimeter, 57.99, 58.01);
    }

    [Fact]
    public void EventosEInutilizacaoUsamDocumentosTipados()
    {
        var evento = NFeEvento.Cancelamento(Fixtures.Nota().Chave, "12345678000195", "135260000000001",
            "Cancelamento solicitado pelo cliente para teste");
        var gerador = new OpenDANFe();
        var eventos = gerador.GerarEventoPDF(evento);
        using var pdf = Abrir(eventos);
        Assert.Single(pdf.Pages.Cast<PdfPage>());
        Artefato("evento-modernizado.pdf", eventos);
        var inutilizacao = Inutilizacao();
        var bytes = gerador.GerarInutilizacaoPDF(inutilizacao);
        using var comprovante = Abrir(bytes);
        Assert.Single(comprovante.Pages.Cast<PdfPage>());
        Artefato("inutilizacao-modernizada.pdf", bytes);
    }

    [Fact]
    public void PdfPodeExigirSenhaERestringirCopia()
    {
        var bytes = new OpenDANFe(new DANFeConfig
        { SenhaUsuario = "leitura", SenhaProprietario = "administracao", PermitirCopia = false, PermitirImpressao = true }).GerarPDF(Fixtures.Nota());
        Assert.Throws<PdfReaderException>(() => Abrir(bytes));
        using var pdf = PdfReader.Open(new MemoryStream(bytes), "administracao", PdfDocumentOpenMode.Modify);
        Assert.False(pdf.SecuritySettings.PermitExtractContent);
        Assert.True(pdf.SecuritySettings.PermitPrint);
    }

    [Fact]
    public async Task CancelamentoAntecipadoNaoCriaArquivoNemBuscaConfiguracao()
    {
        var arquivo = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".pdf");
        using var cancelamento = new CancellationTokenSource(); cancelamento.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            new OpenDANFe().GravarPDFAsync(arquivo, [new NFeProc { NFe = Fixtures.Nota() }], cancelamento.Token));
        Assert.False(File.Exists(arquivo));
        var configuracoes = new ConfiguracoesPorEmpresa();
        var services = new ServiceCollection();
        services.AddSingleton<IDANFeConfigurationProvider>(configuracoes);
        services.AddDANFePdfSharp();
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var web = scope.ServiceProvider.GetRequiredService<IOpenDANFeWeb>();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            web.GerarPDFAsync(new NFeProc { NFe = Fixtures.Nota() }, "a", cancelamento.Token).AsTask());
        Assert.Equal(0, configuracoes.Chamadas);
    }

    [Fact]
    public async Task WebIsolaConfiguracaoPorEmpresaSemDependerDoClienteDeEmissao()
    {
        var configuracoes = new ConfiguracoesPorEmpresa();
        var services = new ServiceCollection();
        services.AddSingleton<IDANFeConfigurationProvider>(configuracoes);
        services.AddDANFePdfSharp();
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var web = scope.ServiceProvider.GetRequiredService<IOpenDANFeWeb>();
        var nota = new NFeProc { NFe = Fixtures.Nota() };
        var resultados = await Task.WhenAll(web.GerarPDFAsync(nota, "paisagem").AsTask(), web.GerarPDFAsync(nota, "retrato").AsTask());
        using var a = Abrir(resultados[0]); using var b = Abrir(resultados[1]);
        Assert.InRange(a.Pages[0].Width.Millimeter, 296.99, 297.01);
        Assert.InRange(b.Pages[0].Width.Millimeter, 209.99, 210.01);
        var evento = new NFeProcEvento { Evento = NFeEvento.Manifestacao(nota.Chave, "12345678000195", 210210) };
        Assert.StartsWith("%PDF-", Encoding.ASCII.GetString(await web.GerarEventosPDFAsync([evento])));
        Assert.StartsWith("%PDF-", Encoding.ASCII.GetString(await web.GerarInutilizacaoPDFAsync(Inutilizacao())));
        await Assert.ThrowsAsync<ArgumentException>(() => web.GerarPDFAsync(nota, " ").AsTask());
    }

    private static NFeProcInutilizacao Inutilizacao() => new()
    {
        Inutilizacao = new NFeInutilizacao
        {
            InfInut = new NFeInutilizacaoInfo
            {
                CNPJ = "12345678000195", TpAmb = "2", Ano = "26", Modelo = "55", Serie = "1",
                NumeroInicial = "1", NumeroFinal = "3", Justificativa = "Inutilizacao solicitada para teste"
            }
        },
        Retorno = new NFeInutilizacaoResult
        {
            InfInut = new NFeRetInutilizacaoInfo
            { CStat = 102, XMotivo = "Inutilizacao homologada", Protocolo = "135260000000003", DataRecebimento = "2026-10-28T10:01:00-03:00" }
        }
    };
    private static PdfDocument Abrir(byte[] bytes) => PdfReader.Open(new MemoryStream(bytes), PdfDocumentOpenMode.Import);
    private static void Artefato(string nome, byte[] bytes)
    {
        var diretorio = Path.Combine(AppContext.BaseDirectory, "Artifacts"); Directory.CreateDirectory(diretorio);
        File.WriteAllBytes(Path.Combine(diretorio, nome), bytes);
    }
    private sealed class GeradorQueAlteraConfiguracao : OpenDANFe
    {
        protected override void RenderizarNota(PdfDocument documento, NFeProc nota, DANFeConfig configuracao, CancellationToken cancellationToken)
        {
            Configuracoes.LarguraNFCeMilimetros = 58;
            base.RenderizarNota(documento, nota, configuracao, cancellationToken);
        }
    }
    private sealed class ConfiguracoesPorEmpresa : IDANFeConfigurationProvider
    {
        internal int Chamadas;
        public async ValueTask<DANFeConfig> GetAsync(string empresa, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref Chamadas);
            await Task.Yield();
            cancellationToken.ThrowIfCancellationRequested();
            return new DANFeConfig { Paisagem = empresa == "paisagem", MensagemRodape = empresa };
        }
    }
}
