using TUnit.Assertions.Enums;
using System.IO;
using System.Text;
using OpenAC.Net.Devices;
using OpenAC.Net.EscPos;
using OpenAC.Net.EscPos.Commom;
using OpenAC.Net.NFe.Common.Model;
using OpenAC.Net.NFe.DANFe.EscPos;
using OpenAC.Net.NFe.DANFe.EscPos.Configuracao;
using System.Threading.Tasks;

namespace OpenAC.Net.NFe.Tests;

public sealed class EscPosIntegrationTests
{
    [Test]
    [Arguments(ProtocoloEscPos.EscPos, PaginaCodigo.pc850)]
    [Arguments(ProtocoloEscPos.EscBema, PaginaCodigo.pc1252)]
    public async Task ExportacaoEmMemoriaProduzOsMesmosBytesDoDispositivo(ProtocoloEscPos protocolo, PaginaCodigo pagina)
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
                await Assert.That(impressora.Conectado).IsTrue();
                await Assert.That(impressora.LinhasEntreCupons).IsEqualTo(9);
                await Assert.That(impressora.Colunas).IsEqualTo(32);
                await Assert.That(impressora.PaginaCodigo).IsEqualTo(pagina);
                await Assert.That(impressora.Protocolo).IsEqualTo(protocolo);
            }
            await Assert.That(File.ReadAllBytes(arquivo)).IsEquivalentTo(esperado, CollectionOrdering.Matching);
            await Assert.That(documento.GetXml()).IsEqualTo(original);
            await Assert.That(documento.ValidarAssinatura()).IsTrue();
        }
        finally { File.Delete(arquivo); }
    }

    [Test]
    [Arguments(32)]
    [Arguments(48)]
    public async Task PreviaUsaRenderizadorDoComponenteSemAbrirDispositivo(int colunas)
    {
        using var certificado = Fixtures.Certificate();
        var nota = Fixtures.Nota(65);
        nota.Assinar(certificado);
        nota.GerarQrCode(Fixtures.Config(certificado, 65));
        var danfe = new OpenDANFeEscPos(new DANFeEscPosConfig
        { Colunas = colunas, MensagemRodape = "OBRIGADO PELA PREFERENCIA" });
        var bytes = danfe.GerarPreview(nota.ComProtocolo(Fixtures.Protocolo(nota)));
        await Assert.That(bytes.Take(8)).IsEquivalentTo(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }, CollectionOrdering.Matching);
        await Assert.That(bytes.Length > 1000).IsTrue();
        var diretorio = Path.Combine(AppContext.BaseDirectory, "Artifacts");
        Directory.CreateDirectory(diretorio);
        File.WriteAllBytes(Path.Combine(diretorio, $"nfce-escpos-{colunas}.png"), bytes);
    }

    [Test]
    [Arguments(32)]
    [Arguments(48)]
    public async Task LayoutPreservaTodosOsProdutosEOrdenaQrCodeAntesDasInformacoesAdicionais(int colunas)
    {
        using var certificado = Fixtures.Certificate();
        var nota = Fixtures.Nota(65, itens: 12);
        nota.InfNFe.InfAdic.InfCpl = "OBSERVACAO-DO-CONTRIBUINTE";
        nota.Assinar(certificado);
        nota.GerarQrCode(Fixtures.Config(certificado, 65));
        var danfe = new OpenDANFeEscPos(new DANFeEscPosConfig { Colunas = colunas });
        var texto = danfe.GerarTexto(nota);
        foreach (var linha in texto.Split('\n'))
            await Assert.That(linha.Length).IsBetween(0, colunas).Inclusive();
        for (var numero = 1; numero <= 12; numero++) await Assert.That(texto).Contains(numero.ToString("D4"));
        await Assert.That(texto.IndexOf("Qtd. total de itens", StringComparison.Ordinal) > texto.IndexOf("0012", StringComparison.Ordinal)).IsTrue();
        var bytes = Encoding.GetEncoding(850).GetString(danfe.Gerar(nota));
        await Assert.That(bytes.IndexOf(nota.QrCode, StringComparison.Ordinal) < bytes.IndexOf("OBSERVACAO-DO-CONTRIBUINTE", StringComparison.Ordinal)).IsTrue();
        await Assert.That(texto).Contains("Dinheiro");
        await Assert.That(texto).Contains("120,00");
    }

    [Test]
    public async Task DadosFiscaisNaoPodemInjetarComandosDeControle()
    {
        var nota = Fixtures.Nota(65);
        nota.InfNFe.Emit.XNome = "LOJA\u001b@INJETADO";
        nota.InfNFe.Det[0].Prod.XProd = "PRODUTO\u001dV\0INJETADO";
        var danfe = new OpenDANFeEscPos();
        var texto = danfe.GerarTexto(nota);
        await Assert.That(texto).DoesNotContain('\u001b');
        await Assert.That(texto).DoesNotContain('\u001d');
        await Assert.That(texto).DoesNotContain('\0');
        var bytes = Encoding.GetEncoding(850).GetString(danfe.Gerar(nota));
        await Assert.That(bytes).Contains("LOJA @INJETADO");
        await Assert.That(bytes).Contains("PRODUTO V INJETADO");
    }

    [Test]
    public async Task PreparacaoUsaColunasDoComponenteERecusaModeloInvalidoSemAlterarBuffer()
    {
        var arquivo = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".bin");
        using var impressora = new EscPosPrinter<FileConfig>(new FileConfig(arquivo)) { Colunas = 32 };
        var danfe = new OpenDANFeEscPos();
        danfe.PrepararCupom(Fixtures.Nota(65), impressora);
        await Assert.That(impressora.Conectado).IsFalse();
        await Assert.That(File.Exists(arquivo)).IsFalse();
        var antes = impressora.GerarPreview();
        await Assert.That(() => danfe.PrepararCupom(Fixtures.Nota(55), impressora)).ThrowsExactly<ArgumentException>();
        await Assert.That(impressora.GerarPreview()).IsEquivalentTo(antes, CollectionOrdering.Matching);
    }

    [Test]
    public async Task ResumoERodapeSaoAplicadosSemAlterarConfiguracaoOriginal()
    {
        var configuracao = new DANFeEscPosConfig { ExibirItens = false, MensagemRodape = "RODAPE-ORIGINAL", Colunas = 32 };
        var danfe = new OpenDANFeEscPos(configuracao);
        configuracao.ExibirItens = true;
        configuracao.MensagemRodape = "RODAPE-ALTERADO";
        var texto = danfe.GerarTexto(Fixtures.Nota(65));
        await Assert.That(texto).DoesNotContain("CÓDIGO / DESCRIÇÃO");
        await Assert.That(texto).Contains("Qtd. total de itens");
        await Assert.That(texto).Contains("RODAPE-ORIGINAL");
        await Assert.That(texto).DoesNotContain("RODAPE-ALTERADO");
    }

    [Test]
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
            await Assert.That(cancelada.IsCompleted).IsFalse();
            cancelamento.Cancel();
            await Assert.That(() => cancelada).Throws<OperationCanceledException>();
            proximo.Configuracoes.MensagemRodape = "CONFIGURACAO-ANTES-DA-FILA";
            var aguardando = proximo.ImprimirAsync(Fixtures.Nota(65), impressora);
            proximo.Configuracoes.MensagemRodape = "CONFIGURACAO-DEPOIS-DA-FILA";
            primeiro.Liberar.TrySetResult();
            await Task.WhenAll(tarefaInicial, aguardando).WaitAsync(TimeSpan.FromSeconds(5));
            var bytes = Encoding.GetEncoding(850).GetString(File.ReadAllBytes(arquivo));
            await Assert.That(bytes).Contains("CONFIGURACAO-ANTES-DA-FILA");
            await Assert.That(bytes).DoesNotContain("CONFIGURACAO-DEPOIS-DA-FILA");
            await Assert.That(impressora.Conectado).IsTrue();
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