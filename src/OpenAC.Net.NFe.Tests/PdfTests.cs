using System.IO;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using OpenAC.Net.NFe.Common.Model;
using OpenAC.Net.NFe.DANFe.PDFSharp;
using OpenAC.Net.NFe.DANFe.PDFSharp.Configuracao;
using OpenAC.Net.NFe.DANFe.PDFSharp.Web;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using System.Threading.Tasks;

namespace OpenAC.Net.NFe.Tests;

public sealed class PdfTests
{
    [Test]
    [Arguments(false, 210, 297)]
    [Arguments(true, 297, 210)]
    public async Task A4RespeitaOrientacaoEUsaMetadadosDaBiblioteca(bool paisagem, int largura, int altura)
    {
        var gerador = new OpenDANFe(new DANFeConfig { Paisagem = paisagem });
        var bytes = gerador.GerarPDF(Fixtures.Nota());
        using var pdf = Abrir(bytes);
        await Assert.That(pdf.Pages.Cast<PdfPage>()).HasSingleItem();
        await Assert.That(pdf.Pages[0].Width.Millimeter).IsBetween(largura - .01,largura + .01).Inclusive();
        await Assert.That(pdf.Pages[0].Height.Millimeter).IsBetween(altura - .01,altura + .01).Inclusive();
        await Assert.That(pdf.Info.Creator).IsEqualTo("OpenAC.Net.NFe.DANFe.PDFSharp");
    }

    [Test]
    [Arguments(58)]
    [Arguments(80)]
    public async Task CupomAjustaAlturaAoConteudoEProduzArtefato(int largura)
    {
        using var certificado = Fixtures.Certificate();
        var nota = Fixtures.Nota(65, itens: 20);
        nota.Assinar(certificado);
        nota.GerarQrCode(Fixtures.Config(certificado, 65));
        var original = nota.GetXml();
        var gerador = new OpenDANFe(new DANFeConfig { LarguraNFCeMilimetros = largura });
        var bytes = gerador.GerarPDF(nota.ComProtocolo(Fixtures.Protocolo(nota)));
        using var pdf = Abrir(bytes);
        await Assert.That(pdf.Pages.Cast<PdfPage>()).HasSingleItem();
        await Assert.That(pdf.Pages[0].Width.Millimeter).IsBetween(largura - .01,largura + .01).Inclusive();
        await Assert.That(pdf.Pages[0].Height.Point).IsBetween(400,5000).Inclusive();
        await Assert.That(nota.GetXml()).IsEqualTo(original);
        await Assert.That(nota.ValidarAssinatura()).IsTrue();
        Artefato($"nfce-modernizada-{largura}.pdf", bytes);
    }

    [Test]
    public async Task DescricaoMuitoLongaContinuaEmOutrasPaginas()
    {
        var nota = Fixtures.Nota();
        nota.InfNFe.Det[0].Prod.XProd = string.Join(" ", Enumerable.Repeat("CONTINUACAO DO PRODUTO", 1200)) + " FIM-DO-PRODUTO";
        nota.InfNFe.InfAdic.InfCpl = "OBSERVACAO-APOS-O-PRODUTO";
        var bytes = new OpenDANFe().GerarPDF(nota);
        using var pdf = Abrir(bytes);
        await Assert.That(pdf.PageCount > 3).IsTrue();
        foreach (var pagina in pdf.Pages.Cast<PdfPage>())
            await Assert.That(pagina.Height.Millimeter).IsBetween(296.99, 297.01).Inclusive();
        Artefato("danfe-produto-longo.pdf", bytes);
    }

    [Test]
    public async Task CupomExtensoContinuaDentroDoLimiteDePaginaDoPdf()
    {
        var gerador = new OpenDANFe(new DANFeConfig { LarguraNFCeMilimetros = 58 });
        var bytes = gerador.GerarPDF(Fixtures.Nota(65, itens: 600));
        using var pdf = Abrir(bytes);
        await Assert.That(pdf.PageCount > 1).IsTrue();
        foreach (var pagina in pdf.Pages.Cast<PdfPage>())
        {
            await Assert.That(pagina.Width.Millimeter).IsBetween(57.99, 58.01).Inclusive();
            await Assert.That(pagina.Height.Point).IsBetween(120, 14400).Inclusive();
        }
        Artefato("nfce-cupom-extenso.pdf", bytes);
    }

    [Test]
    public async Task LogoEmMemoriaFuncionaSemArquivoOuInterfaceGrafica()
    {
        var configuracao = new DANFeConfig
        {
            Logo = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAABAAAAAICAIAAAB/FOjAAAAAE0lEQVR4nGNQCJhAEmIY1UALDQDPN4ABfbuYvwAAAABJRU5ErkJggg=="),
            MensagemRodape = "EMPRESA DE TESTE"
        };
        var bytes = new OpenDANFe(configuracao).GerarPDF(Fixtures.Nota());
        using var pdf = Abrir(bytes);
        await Assert.That(pdf.Pages[0].Resources.Elements.ContainsKey("/XObject")).IsTrue();
        Artefato("danfe-com-logo.pdf", bytes);
    }

    [Test]
    public async Task LotePodeConterNFeENFCeComPapeisDiferentes()
    {
        var primeira = Fixtures.Nota();
        var segunda = Fixtures.Nota(65);
        var bytes = new OpenDANFe().GerarPDF(new NFeProc { NFe = primeira }, new NFeProc { NFe = segunda });
        using var pdf = Abrir(bytes);
        await Assert.That(pdf.PageCount).IsEqualTo(2);
        await Assert.That(pdf.Pages[0].Width.Millimeter).IsBetween(209.99,210.01).Inclusive();
        await Assert.That(pdf.Pages[1].Width.Millimeter).IsBetween(79.99,80.01).Inclusive();
    }

    [Test]
    public async Task ConfiguracaoEClonadaPorOperacao()
    {
        var configuracao = new DANFeConfig { LarguraNFCeMilimetros = 80, Logo = [1, 2, 3] };
        var copia = configuracao.Copiar();
        configuracao.Logo[0] = 9;
        await Assert.That(copia.Logo[0]).IsEqualTo(1);
        var gerador = new GeradorQueAlteraConfiguracao();
        var nota = Fixtures.Nota(65);
        var bytes = gerador.GerarPDF(new NFeProc { NFe = nota }, new NFeProc { NFe = nota });
        using var pdf = Abrir(bytes);
        foreach (var pagina in pdf.Pages.Cast<PdfPage>())
            await Assert.That(pagina.Width.Millimeter).IsBetween(79.99, 80.01).Inclusive();
        using var proximo = Abrir(gerador.GerarPDF(nota));
        await Assert.That(proximo.Pages[0].Width.Millimeter).IsBetween(57.99,58.01).Inclusive();
    }

    [Test]
    public async Task EventosEInutilizacaoUsamDocumentosTipados()
    {
        var evento = NFeEvento.Cancelamento(Fixtures.Nota().Chave, "12345678000195", "135260000000001",
            "Cancelamento solicitado pelo cliente para teste");
        var gerador = new OpenDANFe();
        var eventos = gerador.GerarEventoPDF(evento);
        using var pdf = Abrir(eventos);
        await Assert.That(pdf.Pages.Cast<PdfPage>()).HasSingleItem();
        Artefato("evento-modernizado.pdf", eventos);
        var inutilizacao = Inutilizacao();
        var bytes = gerador.GerarInutilizacaoPDF(inutilizacao);
        using var comprovante = Abrir(bytes);
        await Assert.That(comprovante.Pages.Cast<PdfPage>()).HasSingleItem();
        Artefato("inutilizacao-modernizada.pdf", bytes);
    }

    [Test]
    public async Task PdfPodeExigirSenhaERestringirCopia()
    {
        var bytes = new OpenDANFe(new DANFeConfig
        { SenhaUsuario = "leitura", SenhaProprietario = "administracao", PermitirCopia = false, PermitirImpressao = true }).GerarPDF(Fixtures.Nota());
        await Assert.That(() => Abrir(bytes)).ThrowsExactly<PdfReaderException>();
        using var pdf = PdfReader.Open(new MemoryStream(bytes), "administracao", PdfDocumentOpenMode.Modify);
        await Assert.That(pdf.SecuritySettings.PermitExtractContent).IsFalse();
        await Assert.That(pdf.SecuritySettings.PermitPrint).IsTrue();
    }

    [Test]
    public async Task CancelamentoAntecipadoNaoCriaArquivoNemBuscaConfiguracao()
    {
        var arquivo = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".pdf");
        using var cancelamento = new CancellationTokenSource(); cancelamento.Cancel();
        await Assert.That(() =>
            new OpenDANFe().GravarPDFAsync(arquivo, [new NFeProc { NFe = Fixtures.Nota() }], cancelamento.Token)).Throws<OperationCanceledException>();
        await Assert.That(File.Exists(arquivo)).IsFalse();
        var configuracoes = new ConfiguracoesPorEmpresa();
        var services = new ServiceCollection();
        services.AddSingleton<IDANFeConfigurationProvider>(configuracoes);
        services.AddDANFePdfSharp();
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var web = scope.ServiceProvider.GetRequiredService<IOpenDANFeWeb>();
        await Assert.That(() =>
            web.GerarPDFAsync(new NFeProc { NFe = Fixtures.Nota() }, "a", cancelamento.Token).AsTask()).Throws<OperationCanceledException>();
        await Assert.That(configuracoes.Chamadas).IsEqualTo(0);
    }

    [Test]
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
        await Assert.That(a.Pages[0].Width.Millimeter).IsBetween(296.99,297.01).Inclusive();
        await Assert.That(b.Pages[0].Width.Millimeter).IsBetween(209.99,210.01).Inclusive();
        var evento = new NFeProcEvento { Evento = NFeEvento.Manifestacao(nota.Chave, "12345678000195", 210210) };
        await Assert.That(Encoding.ASCII.GetString(await web.GerarEventosPDFAsync([evento]))).StartsWith("%PDF-");
        await Assert.That(Encoding.ASCII.GetString(await web.GerarInutilizacaoPDFAsync(Inutilizacao()))).StartsWith("%PDF-");
        await Assert.That(() => web.GerarPDFAsync(nota, " ").AsTask()).ThrowsExactly<ArgumentException>();
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