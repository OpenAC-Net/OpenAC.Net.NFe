using System.Text;
using Avalonia.Platform.Storage;
using OpenAC.Net.NFe.Certificates;
using OpenAC.Net.NFe.Common.Model;
using OpenAC.Net.NFe.Common.Types;

namespace OpenAC.Net.NFe.Demo;

public partial class MainWindow
{
    private async Task LoadDocumentsAsync(CancellationToken token)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Carregar NF-e ou NFC-e", AllowMultiple = true,
            FileTypeFilter = [new FilePickerFileType("Documentos XML") { Patterns = ["*.xml"] }]
        });
        if (files.Count == 0) return;
        var xmls = new List<string>();
        foreach (var file in files)
        {
            await using var input = await file.OpenReadAsync();
            using var reader = new StreamReader(input);
            xmls.Add(await reader.ReadToEndAsync(token));
        }
        token.ThrowIfCancellationRequested();
        documents.Replace(xmls);
        RefreshDocuments();
        ResultText.Text = $"{files.Count} documento(s) carregado(s).\n\n" + string.Join("\n", files.Select(file => file.Name))
            + "\n\nConfira modelo, UF, ambiente e emissão nas configurações antes de assinar ou enviar. XMLs 3.10 são aceitos apenas para operações locais.";
        ResponseTabs.SelectedIndex = 0;
    }

    private void RefreshDocuments()
    {
        DocumentCountText.Text = $"{documents.Items.Count} XML";
        DocumentNameText.Text = string.Join(", ", documents.Items.Select(item => $"{item.Modelo} {item.NFe.InfNFe.Ide.NNF}"));
        AccessKeyText.Text = documents.Items.FirstOrDefault()?.Chave ?? "";
        SentXmlText.Text = string.Join("\n\n", documents.Items.Select(item => PrettyXml(DemoDocuments.Xml(item))));
        ReceiptPreview.Source = null;
        preview?.Dispose();
        preview = null;
    }

    private async Task ValidateDocumentsAsync(CancellationToken token)
    {
        documents.Require();
        var schemas = SchemasPathText.Text?.Trim() ?? "";
        await Task.Run(() =>
        {
            foreach (var item in documents.Items)
            {
                token.ThrowIfCancellationRequested();
                item.NFe.ValidarIdentificacao();
                if (item.ProtNFe == null) item.NFe.Validar(schemas);
                else item.Validar(schemas);
                if (item.NFe.Assinado && !item.ValidarAssinatura())
                    throw new InvalidOperationException("Assinatura inválida na NF-e " + item.Chave);
            }
        }, token);
        ResultText.Text = "XMLs validados pelos schemas da versão de cada documento. As assinaturas presentes também foram verificadas.";
        ResponseTabs.SelectedIndex = 0;
    }

    private async Task SignDocumentsAsync(CancellationToken token)
    {
        var config = CreateConfiguration();
        documents.RequireMatching(config);
        if (documents.Items.Any(item => item.ProtNFe != null))
            throw new InvalidOperationException("Documentos processados não podem ser assinados novamente.");
        var copies = documents.Items.Select(item => DemoDocuments.Parse(DemoDocuments.Xml(item))).ToArray();
        await Task.Run(async () =>
        {
            using var certificate = await new NFeCertificadoProvider().ObterAsync(config.Certificados, token);
            foreach (var item in copies)
            {
                token.ThrowIfCancellationRequested();
                if (!item.NFe.Assinado) item.NFe.Assinar(certificate);
                if (!item.NFe.ValidarAssinatura()) throw new InvalidOperationException("O XML já contém uma assinatura inválida.");
                if (item.Modelo == ModeloNFe.NFCe && item.Versao == NFeVersao.v400) item.NFe.GerarQrCode(config);
            }
        }, token);
        token.ThrowIfCancellationRequested();
        documents.Replace(copies.Select(DemoDocuments.Xml));
        RefreshDocuments();
        ResultText.Text = "Documentos assinados. Para NFC-e 4.00, o QR Code foi gerado conforme as configurações. Use Salvar XMLs para exportar.";
        ResponseTabs.SelectedIndex = 0;
    }

    private async Task SaveDocumentsAsync(CancellationToken token)
    {
        documents.Require();
        if (documents.Items.Count == 1)
        {
            var destination = await PickOutputAsync("Salvar XML", XmlName(documents.Items[0], 1), "xml");
            if (destination == null) return;
            await WriteAsync(destination, Encoding.UTF8.GetBytes(DemoDocuments.Xml(documents.Items[0])), token);
        }
        else
        {
            var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = "Salvar XMLs do lote" });
            if (folders.Count == 0) return;
            for (var i = 0; i < documents.Items.Count; i++)
            {
                token.ThrowIfCancellationRequested();
                using var file = await folders[0].CreateFileAsync(XmlName(documents.Items[i], i + 1))
                    ?? throw new IOException("Não foi possível criar o arquivo XML.");
                await WriteAsync(file, Encoding.UTF8.GetBytes(DemoDocuments.Xml(documents.Items[i])), token);
            }
        }
        ResultText.Text = "XMLs exportados. Documentos autorizados incluem o protocolo de autorização.";
        ResponseTabs.SelectedIndex = 0;
    }

    private static string XmlName(NFeProc item, int index)
        => (item.Chave.Length == 44 && item.Chave.All(char.IsAsciiDigit) ? item.Chave : $"nota-{index}")
            + (item.ProtNFe == null ? "-nfe.xml" : "-procNFe.xml");
}
