using TUnit.Assertions.Enums;
using System.IO;
using System.Text;
using OpenAC.Net.Devices;
using OpenAC.Net.EscPos;
using OpenAC.Net.EscPos.Commom;
using OpenAC.Net.NFe.DANFe.EscPos;
using System.Threading.Tasks;

namespace OpenAC.Net.NFe.Tests;

public sealed class EscPosTests
{
    static EscPosTests() => Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

    [Test]
    [Arguments(PaginaCodigo.pc850, 850)]
    [Arguments(PaginaCodigo.pc1252, 1252)]
    [Arguments(PaginaCodigo.pcUTF8, 65001)]
    public async Task GeracaoUsaCodificacaoDoComponente(PaginaCodigo pagina, int codigo)
    {
        var bytes = new OpenDANFeEscPos().Gerar(Fixtures.Nota(65), paginaCodigo: pagina);
        await Assert.That(Encoding.GetEncoding(codigo).GetString(bytes)).Contains("Eletrônica");
    }

    [Test]
    [Arguments(0)]
    [Arguments(5)]
    [Arguments(255)]
    public async Task SemCorteRespeitaQuantidadeDeLinhasDeAvanco(int linhas)
    {
        var nota = Fixtures.Nota(65);
        var printer = new OpenDANFeEscPos();
        printer.Configuracoes.CortarPapel = false;
        printer.Configuracoes.LinhasAvanco = linhas;
        var texto = Encoding.ASCII.GetString(printer.Gerar(nota));

        await Assert.That(texto.Reverse().TakeWhile(c => c == '\n').Count()).IsEqualTo(linhas + 1);
        await Assert.That(texto).DoesNotContain("\u001dV\0");
    }

    [Test]
    [Arguments(1)]
    [Arguments(4)]
    [Arguments(8)]
    public async Task QrCodePreservaModuloModeloECorrecao(int modulo)
    {
        using var cert = Fixtures.Certificate();
        var nota = Fixtures.Nota(65);
        nota.Assinar(cert);
        nota.GerarQrCode(Fixtures.Config(cert, 65));
        var printer = new OpenDANFeEscPos();
        printer.Configuracoes.ModuloQrCode = modulo;
        var bytes = Encoding.UTF8.GetString(printer.Gerar(nota));

        await Assert.That(bytes).Contains("\u001d(k\u0004\0\u0031A\u0032\0");
        await Assert.That(bytes).Contains("\u001d(k\u0003\0\u0031C" + (char)modulo);
        await Assert.That(bytes).Contains("\u001d(k\u0003\0\u0031E\u0031");
        await Assert.That(bytes).Contains(nota.QrCode);
    }

    [Test]
    [Arguments(ProtocoloEscPos.EscPos, "\u001dV\0")]
    [Arguments(ProtocoloEscPos.EscBema, "\u001bw")]
    public async Task ImpressaoUsaDispositivoProtocoloECodificacaoDoComponente(ProtocoloEscPos protocolo, string corte)
    {
        var arquivo = Path.GetTempFileName();
        try
        {
            var nota = Fixtures.Nota(65);
            using var cert = Fixtures.Certificate();
            nota.Assinar(cert);
            nota.GerarQrCode(Fixtures.Config(cert, 65));
            var danfe = new OpenDANFeEscPos();
            using var impressora = new EscPosPrinter<FileConfig>(new FileConfig(arquivo));
            impressora.Protocolo = protocolo;
            impressora.PaginaCodigo = PaginaCodigo.pc850;
            impressora.LinhasEntreCupons = 9;
            impressora.Conectar();
            danfe.Imprimir(nota, impressora);

            await Assert.That(impressora.Conectado).IsTrue();
            await Assert.That(impressora.LinhasEntreCupons).IsEqualTo(9);
            var bytes = File.ReadAllBytes(arquivo);
            var texto = Encoding.GetEncoding(850).GetString(bytes);
            await Assert.That(texto).Contains("Eletrônica");
            await Assert.That(texto).Contains("Dinheiro");
            await Assert.That(texto).Contains(nota.QrCode);
            await Assert.That(texto).EndsWith(corte);

            await Assert.That(() =>
                danfe.ImprimirAsync(nota, impressora, new CancellationToken(true))).Throws<OperationCanceledException>();
            await Assert.That(File.ReadAllBytes(arquivo)).IsEquivalentTo(bytes, CollectionOrdering.Matching);

            danfe.Configuracoes.CortarPapel = false;
            await danfe.ImprimirAsync(nota, impressora);
            await Assert.That(impressora.Conectado).IsTrue();
            var segundoCupom = Encoding.GetEncoding(850).GetString(File.ReadAllBytes(arquivo).Skip(bytes.Length).ToArray());
            await Assert.That(segundoCupom).Contains("Dinheiro");
            await Assert.That(segundoCupom).DoesNotContain(corte);
        }
        finally
        {
            File.Delete(arquivo);
        }
    }

    [Test]
    public async Task ImpressaoExigeComponenteConectado()
    {
        using var impressora = new EscPosPrinter<FileConfig>(new FileConfig("nao-deve-ser-criado.bin"));
        await Assert.That(() => new OpenDANFeEscPos().Imprimir(Fixtures.Nota(65), impressora)).ThrowsExactly<InvalidOperationException>();
    }
}