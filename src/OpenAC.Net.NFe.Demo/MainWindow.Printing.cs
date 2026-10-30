using System.Xml.Linq;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using OpenAC.Net.NFe.Common;
using OpenAC.Net.NFe.Common.Model;
using OpenAC.Net.NFe.Common.Types;
using OpenAC.Net.NFe.DANFe.EscPos;
using OpenAC.Net.NFe.DANFe.EscPos.Configuracao;
using OpenAC.Net.NFe.DANFe.PDFSharp;
using OpenAC.Net.NFe.DANFe.PDFSharp.Configuracao;

namespace OpenAC.Net.NFe.Demo;

public partial class MainWindow
{
    private OpenDANFe PdfRenderer() => new(new DANFeConfig
    {
        Paisagem = Selected<bool>(PdfOrientationCombo), LarguraNFCeMilimetros = Selected<int>(ReceiptWidthCombo)
    });

    private OpenDANFeEscPos ReceiptRenderer() => new(new DANFeEscPosConfig { Colunas = Selected<int>(ColumnsCombo) });

    private async Task ExportPdfAsync(CancellationToken token)
    {
        documents.Require();
        var destination = await PickOutputAsync("Salvar DANFE PDF", "danfe.pdf", "pdf");
        if (destination == null) return;
        var renderer = PdfRenderer();
        var bytes = await Task.Run(() => renderer.GerarPDF(token, documents.Items.ToArray()), token);
        await SavePdfAsync(destination, bytes, token);
    }

    private async Task ExportEventPdfAsync(CancellationToken token)
    {
        var source = await PickFileAsync("Selecionar evento ou inutilização", "Documento XML", "*.xml");
        if (source == null) return;
        string xml;
        await using (var input = await source.OpenReadAsync())
        {
            using var reader = new StreamReader(input);
            xml = await reader.ReadToEndAsync(token);
        }
        var destination = await PickOutputAsync("Salvar PDF", Path.GetFileNameWithoutExtension(source.Name) + ".pdf", "pdf");
        if (destination == null) return;
        var renderer = PdfRenderer();
        var bytes = await Task.Run(() =>
        {
            var root = XElement.Parse(xml);
            XNamespace ns = NFeConst.Namespace;
            if (root.Name == ns + "procEventoNFe") return renderer.GerarEventoPDF(token, NFeProcEvento.ReadFromXml(root));
            if (root.Name == ns + "evento") return renderer.GerarEventoPDF(token, new NFeProcEvento { Evento = NFeEvento.ReadFromXml(root) });
            if (root.Name == ns + "procInutNFe") return renderer.GerarInutilizacaoPDF(NFeProcInutilizacao.ReadFromXml(root), token);
            throw new ArgumentException("Esperado evento, procEventoNFe ou procInutNFe no namespace oficial.");
        }, token);
        await SavePdfAsync(destination, bytes, token);
    }

    private async Task SavePdfAsync(IStorageFile destination, byte[] bytes, CancellationToken token)
    {
        await WriteAsync(destination, bytes, token);
        ResultText.Text = $"PDF gerado: {destination.TryGetLocalPath() ?? destination.Name}\nTamanho: {bytes.Length:N0} bytes";
        ResponseTabs.SelectedIndex = 0;
        if (OpenPdfCheck.IsChecked == true && !await Launcher.LaunchFileAsync(destination))
            AppendLog("PDF salvo. Abra o arquivo pelo leitor de sua preferência.");
    }

    private NFeProc SingleReceipt()
    {
        documents.Require();
        if (documents.Items.Count != 1 || documents.Items[0].Modelo != ModeloNFe.NFCe)
            throw new InvalidOperationException("Carregue uma única NFC-e modelo 65 para gerar o cupom.");
        return documents.Items[0];
    }

    private async Task PreviewAsync(CancellationToken token)
    {
        var note = SingleReceipt();
        var renderer = ReceiptRenderer();
        var bytes = await Task.Run(() => renderer.GerarPreview(note, token), token);
        token.ThrowIfCancellationRequested();
        using var input = new MemoryStream(bytes);
        var image = new Bitmap(input);
        ReceiptPreview.Source = null;
        preview?.Dispose();
        preview = image;
        ReceiptPreview.Source = image;
        ResponseTabs.SelectedIndex = 4;
        var destination = await PickOutputAsync("Salvar prévia NFC-e (opcional)", "nfce.png", "png");
        if (destination != null) await WriteAsync(destination, bytes, token);
        ResultText.Text = "Prévia NFC-e gerada. Consulte a aba Prévia NFC-e.";
    }

    private async Task ExportEscPosAsync(CancellationToken token)
    {
        var note = SingleReceipt();
        var destination = await PickOutputAsync("Salvar comandos ESC/POS", "nfce.bin", "bin");
        if (destination == null) return;
        var renderer = ReceiptRenderer();
        var bytes = await Task.Run(() => renderer.Gerar(note, cancellationToken: token), token);
        await WriteAsync(destination, bytes, token);
        ResultText.Text = $"Arquivo ESC/POS gerado: {destination.Name}\nTamanho: {bytes.Length:N0} bytes";
        ResponseTabs.SelectedIndex = 0;
    }
}
