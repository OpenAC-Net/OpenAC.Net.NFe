using System.Globalization;
using OpenAC.Net.NFe.Common.Model;

namespace OpenAC.Net.NFe.Demo;

public partial class MainWindow
{
    private async Task SendDocumentsAsync(CancellationToken token)
    {
        var config = CreateConfiguration();
        documents.RequireSendable(config, settings.Synchronous);
        var response = await Client().EnviarAsync(new NFeEnvioSolicitacao
        {
            Lote = settings.Batch, Sincrono = settings.Synchronous,
            Documentos = documents.Items.Select(item => item.NFe).ToArray()
        }, token);
        documents.Sent = response.DocumentosEnviados;
        documents.Receipt = response.Recibo;
        // Keep the signed copies for receipt processing and later export, including rejected documents.
        for (var i = 0; i < documents.Sent.Count; i++)
        {
            var sent = documents.Sent[i];
            var index = documents.Items.FindIndex(item => item.Chave == sent.Chave);
            if (index >= 0) documents.Items[index] = new NFeProc { NFe = sent, Versao = sent.InfNFe.Versao };
        }
        documents.Merge(response.Documentos);
        RefreshDocuments();
        ShowResponse(response);
        ResultText.Text = $"Documentos autorizados: {response.Documentos.Count}\nRecibo: {response.Recibo}\n\n" + ResultText.Text;
    }

    private async Task ReceiptAsync(CancellationToken token)
    {
        var values = await InputDialog.AskAsync(this, "Consultar recibo de lote", new InputField("recibo", "Número do recibo", documents.Receipt));
        if (values == null) return;
        token.ThrowIfCancellationRequested();
        var config = CreateConfiguration();
        if (documents.Sent.Count > 0) documents.RequireMatching(config);
        var response = await Client().ConsultaReciboAsync(new NFeReciboSolicitacao
        {
            Recibo = Digits(values["recibo"]), Documentos = documents.Sent
        }, token);
        documents.Merge(response.Documentos);
        if (documents.Items.Count > 0) RefreshDocuments();
        ShowResponse(response);
    }

    private async Task RegistrationAsync(CancellationToken token)
    {
        var values = await InputDialog.AskAsync(this, "Consultar cadastro SEFAZ", new InputField("documento", "CPF, CNPJ ou inscrição estadual"), new InputField("tipo", "Tipo: CPF, CNPJ ou IE", "CNPJ"));
        if (values == null) return;
        token.ThrowIfCancellationRequested();
        ShowResponse(await Client().ConsultaCadastroAsync(Digits(values["documento"]), values["tipo"].ToUpperInvariant(), token));
    }

    private async Task DistributionAsync(CancellationToken token)
    {
        var values = await InputDialog.AskAsync(this, "Distribuição DF-e",
            new InputField("documento", "CPF ou CNPJ do interessado"),
            new InputField("ultimo", "Último NSU (preencha somente um filtro)", "0", false),
            new InputField("nsu", "NSU específico", "", false),
            new InputField("chave", "Chave de acesso", "", false));
        if (values == null) return;
        if (new[] { values["ultimo"], values["nsu"], values["chave"] }.Count(value => !string.IsNullOrWhiteSpace(value)) != 1)
            throw new ArgumentException("Informe somente um filtro: último NSU, NSU específico ou chave de acesso.");
        token.ThrowIfCancellationRequested();
        var response = await Client().DistribuicaoDFeAsync(new NFeDistribuicaoSolicitacao
        {
            CpfCnpj = Digits(values["documento"]), UltimoNsu = OptionalNsu(values["ultimo"]),
            Nsu = OptionalNsu(values["nsu"]), Chave = string.IsNullOrWhiteSpace(values["chave"]) ? null : Digits(values["chave"])
        }, token);
        ShowResponse(response);
        var distributed = await Task.Run(response.DocumentosDistribuidos, token);
        ReceivedXmlText.Text += "\n\n" + string.Join("\n\n", distributed.Select(item => $"NSU {item.NSU} · {item.Schema}\n{PrettyXml(item.Xml)}"));
        ResultText.Text = $"Último NSU: {response.UltNSU}\nMáximo NSU: {response.MaxNSU}\nDocumentos distribuídos: {distributed.Count}\n\n" + ResultText.Text;
    }

    private async Task CancelDocumentAsync(CancellationToken token)
    {
        var note = documents.Items.FirstOrDefault();
        var values = await InputDialog.AskAsync(this, "Cancelar NF-e / NFC-e",
            new InputField("chave", "Chave de acesso", AccessKeyText.Text ?? ""),
            new InputField("protocolo", "Protocolo de autorização", note?.ProtNFe?.InfProt?.NProt ?? ""),
            new InputField("autor", "CPF ou CNPJ do emitente", DefaultAuthor()),
            new InputField("justificativa", "Justificativa do cancelamento"));
        if (values == null) return;
        token.ThrowIfCancellationRequested();
        var response = await Client().EnviarEventoAsync(new NFeEventoSolicitacao
        {
            Lote = BatchText.Text?.Trim() ?? "1",
            Eventos = [NFeEvento.Cancelamento(Digits(values["chave"]), Digits(values["autor"]), Digits(values["protocolo"]), values["justificativa"])]
        }, token);
        foreach (var item in response.Eventos.Where(item => item.RetEvento?.InfEvento?.CStat is 135 or 155))
        {
            var canceled = documents.Items.FirstOrDefault(document => document.Chave == item.Evento.Chave);
            if (canceled != null) canceled.Cancelado = true;
        }
        ShowResponse(response);
    }

    private async Task CorrectionAsync(CancellationToken token)
    {
        var values = await InputDialog.AskAsync(this, "Carta de correção NF-e",
            new InputField("chave", "Chave de acesso", AccessKeyText.Text ?? ""),
            new InputField("autor", "CPF ou CNPJ do emitente", DefaultAuthor()),
            new InputField("sequencia", "Sequência do evento", "1"), new InputField("correcao", "Correção"));
        if (values == null) return;
        token.ThrowIfCancellationRequested();
        ShowResponse(await Client().EnviarEventoAsync(new NFeEventoSolicitacao
        {
            Lote = BatchText.Text?.Trim() ?? "1",
            Eventos = [NFeEvento.CartaCorrecao(Digits(values["chave"]), Digits(values["autor"]), Number(values["sequencia"], "sequência"), values["correcao"])]
        }, token));
    }

    private async Task VoidNumbersAsync(CancellationToken token)
    {
        var values = await InputDialog.AskAsync(this, "Inutilizar numeração",
            new InputField("cnpj", "CNPJ do emitente", DefaultAuthor()), new InputField("ano", "Ano (dois dígitos)", DateTime.Now.ToString("yy", CultureInfo.InvariantCulture)),
            new InputField("serie", "Série", "1"), new InputField("inicio", "Número inicial"), new InputField("fim", "Número final"),
            new InputField("justificativa", "Justificativa da inutilização"));
        if (values == null) return;
        token.ThrowIfCancellationRequested();
        ShowResponse(await Client().InutilizarAsync(new NFeInutilizacaoSolicitacao
        {
            Cnpj = Digits(values["cnpj"]), Ano = Number(values["ano"], "ano", true), Serie = Number(values["serie"], "série", true),
            NumeroInicial = Number(values["inicio"], "número inicial"), NumeroFinal = Number(values["fim"], "número final"), Justificativa = values["justificativa"]
        }, token));
    }

    private string DefaultAuthor()
    {
        var emitter = documents.Items.FirstOrDefault()?.NFe.InfNFe.Emit;
        return string.IsNullOrWhiteSpace(emitter?.CNPJ) ? emitter?.CPF ?? "" : emitter.CNPJ;
    }

    private string AccessKey()
    {
        var key = Digits(AccessKeyText.Text ?? "");
        if (key.Length != 44) throw new ArgumentException("Informe uma chave de acesso com 44 dígitos.");
        return key;
    }

    private static string? OptionalNsu(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        if (!value.All(char.IsAsciiDigit) || value.Length > 15)
            throw new ArgumentException("Informe um NSU com até 15 dígitos.");
        return value.PadLeft(15, '0');
    }

    private static string Digits(string value) => new(value.Where(char.IsAsciiDigit).ToArray());
    private static int Number(string text, string label, bool allowZero = false)
        => int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var value) && value >= (allowZero ? 0 : 1)
            ? value : throw new ArgumentException($"Informe um valor válido para {label}.");
}
