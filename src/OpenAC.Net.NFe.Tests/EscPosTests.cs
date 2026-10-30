using System.Text;
using OpenAC.Net.Devices;
using OpenAC.Net.EscPos;
using OpenAC.Net.EscPos.Commom;
using OpenAC.Net.NFe.DANFe.EscPos;
using Xunit;

namespace OpenAC.Net.NFe.Tests;

public sealed class EscPosTests
{
    static EscPosTests() => Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

    [Theory]
    [InlineData(PaginaCodigo.pc850, 850)]
    [InlineData(PaginaCodigo.pc1252, 1252)]
    [InlineData(PaginaCodigo.pcUTF8, 65001)]
    public void GeracaoUsaCodificacaoDoComponente(PaginaCodigo pagina, int codigo)
    {
        var bytes = new OpenDANFeEscPos().Gerar(Fixtures.Nota(65), paginaCodigo: pagina);
        Assert.Contains("Eletrônica", Encoding.GetEncoding(codigo).GetString(bytes));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(5)]
    [InlineData(255)]
    public void SemCorteRespeitaQuantidadeDeLinhasDeAvanco(int linhas)
    {
        var nota = Fixtures.Nota(65);
        var printer = new OpenDANFeEscPos();
        printer.Configuracoes.CortarPapel = false;
        printer.Configuracoes.LinhasAvanco = linhas;
        var texto = Encoding.ASCII.GetString(printer.Gerar(nota));

        Assert.Equal(linhas + 1, texto.Reverse().TakeWhile(c => c == '\n').Count());
        Assert.DoesNotContain("\u001dV\0", texto, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(4)]
    [InlineData(8)]
    public void QrCodePreservaModuloModeloECorrecao(int modulo)
    {
        using var cert = Fixtures.Certificate();
        var nota = Fixtures.Nota(65);
        nota.Assinar(cert);
        nota.GerarQrCode(Fixtures.Config(cert, 65));
        var printer = new OpenDANFeEscPos();
        printer.Configuracoes.ModuloQrCode = modulo;
        var bytes = Encoding.UTF8.GetString(printer.Gerar(nota));

        Assert.Contains("\u001d(k\u0004\0\u0031A\u0032\0", bytes, StringComparison.Ordinal);
        Assert.Contains("\u001d(k\u0003\0\u0031C" + (char)modulo, bytes, StringComparison.Ordinal);
        Assert.Contains("\u001d(k\u0003\0\u0031E\u0031", bytes, StringComparison.Ordinal);
        Assert.Contains(nota.QrCode, bytes);
    }

    [Theory]
    [InlineData(ProtocoloEscPos.EscPos, "\u001dV\0")]
    [InlineData(ProtocoloEscPos.EscBema, "\u001bw")]
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

            Assert.True(impressora.Conectado);
            Assert.Equal(9, impressora.LinhasEntreCupons);
            var bytes = File.ReadAllBytes(arquivo);
            var texto = Encoding.GetEncoding(850).GetString(bytes);
            Assert.Contains("Eletrônica", texto);
            Assert.Contains("Dinheiro", texto);
            Assert.Contains(nota.QrCode, texto, StringComparison.Ordinal);
            Assert.EndsWith(corte, texto, StringComparison.Ordinal);

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                danfe.ImprimirAsync(nota, impressora, new CancellationToken(true)));
            Assert.Equal(bytes, File.ReadAllBytes(arquivo));

            danfe.Configuracoes.CortarPapel = false;
            await danfe.ImprimirAsync(nota, impressora);
            Assert.True(impressora.Conectado);
            var segundoCupom = Encoding.GetEncoding(850).GetString(File.ReadAllBytes(arquivo).Skip(bytes.Length).ToArray());
            Assert.Contains("Dinheiro", segundoCupom);
            Assert.DoesNotContain(corte, segundoCupom, StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(arquivo);
        }
    }

    [Fact]
    public void ImpressaoExigeComponenteConectado()
    {
        using var impressora = new EscPosPrinter<FileConfig>(new FileConfig("nao-deve-ser-criado.bin"));
        Assert.Throws<InvalidOperationException>(() => new OpenDANFeEscPos().Imprimir(Fixtures.Nota(65), impressora));
    }
}
