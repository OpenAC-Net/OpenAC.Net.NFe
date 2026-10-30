using System.Text;
using OpenAC.Net.Devices;
using OpenAC.Net.EscPos;
using OpenAC.Net.EscPos.Commom;
using OpenAC.Net.NFe.Common.Model;
using OpenAC.Net.NFe.DANFe.EscPos;
using OpenAC.Net.NFe.DANFe.EscPos.Configuracao;
using Xunit;

namespace OpenAC.Net.NFe.Tests;

public sealed class EscPosIntegrationTests
{
    [Theory]
    [InlineData(ProtocoloEscPos.EscPos, PaginaCodigo.pc850)]
    [InlineData(ProtocoloEscPos.EscBema, PaginaCodigo.pc1252)]
    public void ExportacaoEmMemoriaProduzOsMesmosBytesDoDispositivo(ProtocoloEscPos protocolo, PaginaCodigo pagina)
    {
        using var certificado = Fixtures.Certificate();
        var nota = Fixtures.Nota(65);
        nota.Assinar(certificado);
        nota.GerarQrCode(Fixtures.Config(certificado, 65));
        var documento = nota.ComProtocolo(Fixtures.Protocolo(nota));
        var original = documento.GetXml();
        var danfe = new OpenDANFeEscPos(new DANFeEscPosConfig
        { Colunas = 32, Copias = 2, MensagemRodape = "OBRIGADO PELA PREFERENCIA" });
        var esperado = danfe.Gerar(documento, protocolo, pagina);
        var arquivo = Path.GetTempFileName();
        try
        {
            using (var impressora = new EscPosPrinter<FileConfig>(new FileConfig(arquivo))
            { Protocolo = protocolo, PaginaCodigo = pagina, Colunas = 32, LinhasEntreCupons = 9 })
            {
                impressora.Conectar();
                danfe.Imprimir(documento, impressora);
                Assert.True(impressora.Conectado);
                Assert.Equal(9, impressora.LinhasEntreCupons);
                Assert.Equal(32, impressora.Colunas);
                Assert.Equal(pagina, impressora.PaginaCodigo);
                Assert.Equal(protocolo, impressora.Protocolo);
            }
            Assert.Equal(esperado, File.ReadAllBytes(arquivo));
            Assert.Equal(original, documento.GetXml());
            Assert.True(documento.ValidarAssinatura());
        }
        finally { File.Delete(arquivo); }
    }

    [Theory]
    [InlineData(32)]
    [InlineData(48)]
    public void PreviaUsaRenderizadorDoComponenteSemAbrirDispositivo(int colunas)
    {
        using var certificado = Fixtures.Certificate();
        var nota = Fixtures.Nota(65);
        nota.Assinar(certificado);
        nota.GerarQrCode(Fixtures.Config(certificado, 65));
        var danfe = new OpenDANFeEscPos(new DANFeEscPosConfig
        { Colunas = colunas, MensagemRodape = "OBRIGADO PELA PREFERENCIA" });
        var bytes = danfe.GerarPreview(nota.ComProtocolo(Fixtures.Protocolo(nota)));
        Assert.Equal(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }, bytes.Take(8));
        Assert.True(bytes.Length > 1000);
        var diretorio = Path.Combine(AppContext.BaseDirectory, "Artifacts");
        Directory.CreateDirectory(diretorio);
        File.WriteAllBytes(Path.Combine(diretorio, $"nfce-escpos-{colunas}.png"), bytes);
    }

    [Theory]
    [InlineData(32)]
    [InlineData(48)]
    public void LayoutPreservaTodosOsProdutosEOrdenaQrCodeAntesDasInformacoesAdicionais(int colunas)
    {
        using var certificado = Fixtures.Certificate();
        var nota = Fixtures.Nota(65, itens: 12);
        nota.InfNFe.InfAdic.InfCpl = "OBSERVACAO-DO-CONTRIBUINTE";
        nota.Assinar(certificado);
        nota.GerarQrCode(Fixtures.Config(certificado, 65));
        var danfe = new OpenDANFeEscPos(new DANFeEscPosConfig { Colunas = colunas });
        var texto = danfe.GerarTexto(nota);
        Assert.All(texto.Split('\n'), linha => Assert.InRange(linha.Length, 0, colunas));
        for (var numero = 1; numero <= 12; numero++) Assert.Contains(numero.ToString("D4"), texto);
        Assert.True(texto.IndexOf("Qtd. total de itens", StringComparison.Ordinal) > texto.IndexOf("0012", StringComparison.Ordinal));
        var bytes = Encoding.GetEncoding(850).GetString(danfe.Gerar(nota));
        Assert.True(bytes.IndexOf(nota.QrCode, StringComparison.Ordinal) < bytes.IndexOf("OBSERVACAO-DO-CONTRIBUINTE", StringComparison.Ordinal));
        Assert.Contains("Dinheiro", texto);
        Assert.Contains("120,00", texto);
    }

    [Fact]
    public void DadosFiscaisNaoPodemInjetarComandosDeControle()
    {
        var nota = Fixtures.Nota(65);
        nota.InfNFe.Emit.XNome = "LOJA\u001b@INJETADO";
        nota.InfNFe.Det[0].Prod.XProd = "PRODUTO\u001dV\0INJETADO";
        var danfe = new OpenDANFeEscPos();
        var texto = danfe.GerarTexto(nota);
        Assert.DoesNotContain('\u001b', texto);
        Assert.DoesNotContain('\u001d', texto);
        Assert.DoesNotContain('\0', texto);
        var bytes = Encoding.GetEncoding(850).GetString(danfe.Gerar(nota));
        Assert.Contains("LOJA @INJETADO", bytes);
        Assert.Contains("PRODUTO V INJETADO", bytes);
    }

    [Fact]
    public void PreparacaoUsaColunasDoComponenteERecusaModeloInvalidoSemAlterarBuffer()
    {
        var arquivo = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".bin");
        using var impressora = new EscPosPrinter<FileConfig>(new FileConfig(arquivo)) { Colunas = 32 };
        var danfe = new OpenDANFeEscPos();
        danfe.PrepararCupom(Fixtures.Nota(65), impressora);
        Assert.False(impressora.Conectado);
        Assert.False(File.Exists(arquivo));
        var antes = impressora.GerarPreview();
        Assert.Throws<ArgumentException>(() => danfe.PrepararCupom(Fixtures.Nota(55), impressora));
        Assert.Equal(antes, impressora.GerarPreview());
    }

    [Fact]
    public void ResumoERodapeSaoAplicadosSemAlterarConfiguracaoOriginal()
    {
        var configuracao = new DANFeEscPosConfig { ExibirItens = false, MensagemRodape = "RODAPE-ORIGINAL", Colunas = 32 };
        var danfe = new OpenDANFeEscPos(configuracao);
        configuracao.ExibirItens = true;
        configuracao.MensagemRodape = "RODAPE-ALTERADO";
        var texto = danfe.GerarTexto(Fixtures.Nota(65));
        Assert.DoesNotContain("CÓDIGO / DESCRIÇÃO", texto);
        Assert.Contains("Qtd. total de itens", texto);
        Assert.Contains("RODAPE-ORIGINAL", texto);
        Assert.DoesNotContain("RODAPE-ALTERADO", texto);
    }

    [Fact]
    public async Task ChamadasCompartilhamFilaDaImpressoraEPermitemCancelarAEspera()
    {
        var arquivo = Path.GetTempFileName();
        var primeiro = new DanfeBloqueado();
        Task tarefaInicial = null;
        try
        {
            using var impressora = new EscPosPrinter<FileConfig>(new FileConfig(arquivo));
            impressora.Conectar();
            tarefaInicial = Task.Run(() => primeiro.Imprimir(Fixtures.Nota(65), impressora));
            await primeiro.Entrou.Task.WaitAsync(TimeSpan.FromSeconds(5));
            var proximo = new OpenDANFeEscPos();
            using var cancelamento = new CancellationTokenSource();
            var cancelada = proximo.ImprimirAsync(Fixtures.Nota(65), impressora, cancelamento.Token);
            Assert.False(cancelada.IsCompleted);
            cancelamento.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cancelada);
            proximo.Configuracoes.MensagemRodape = "CONFIGURACAO-ANTES-DA-FILA";
            var aguardando = proximo.ImprimirAsync(Fixtures.Nota(65), impressora);
            proximo.Configuracoes.MensagemRodape = "CONFIGURACAO-DEPOIS-DA-FILA";
            primeiro.Liberar.TrySetResult();
            await Task.WhenAll(tarefaInicial, aguardando).WaitAsync(TimeSpan.FromSeconds(5));
            var bytes = Encoding.GetEncoding(850).GetString(File.ReadAllBytes(arquivo));
            Assert.Contains("CONFIGURACAO-ANTES-DA-FILA", bytes);
            Assert.DoesNotContain("CONFIGURACAO-DEPOIS-DA-FILA", bytes);
            Assert.True(impressora.Conectado);
        }
        finally
        {
            primeiro.Liberar.TrySetResult();
            if (tarefaInicial != null) await tarefaInicial.WaitAsync(TimeSpan.FromSeconds(5));
            File.Delete(arquivo);
        }
    }

    private sealed class DanfeBloqueado : OpenDANFeEscPos
    {
        internal TaskCompletionSource Entrou { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource Liberar { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        protected override void RenderizarCupom(NFeProc nota, EscPosPrinter impressora, DANFeEscPosConfig configuracao, CancellationToken cancellationToken)
        {
            Entrou.TrySetResult();
            Liberar.Task.GetAwaiter().GetResult();
            base.RenderizarCupom(nota, impressora, configuracao, cancellationToken);
        }
    }
}
