using System.Collections.ObjectModel;
using System.Xml.Linq;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using OpenAC.Net.DFe.Core.Service;
using OpenAC.Net.NFe.Common;
using OpenAC.Net.NFe.Common.Model;

namespace OpenAC.Net.NFe.Demo;

public partial class MainWindow : Window
{
    private readonly IOpenNFeClient client;
    private readonly string configurationPath;
    private readonly DemoDocuments documents = new();
    private readonly ObservableCollection<ServiceEndpoint> endpoints = [];
    private CancellationTokenSource? operation;
    private DemoSettings settings = new();
    private Bitmap? preview;
    private bool closeAfterOperation;
    private bool initialized;

    public MainWindow() : this(config => new OpenNFe(config), Path.Combine(DemoSettings.Home, "nfe.json")) { }

    internal MainWindow(Func<NFeConfig, IOpenNFeClient> clientFactory, string configurationFile)
    {
        client = clientFactory(new NFeConfig());
        configurationPath = configurationFile;
        InitializeComponent();
        ServiceList.ItemsSource = endpoints;
        InitializeSettings();
        ConnectEvents();
        initialized = true;
        UpdateContext();
        RefreshServices();
    }

    private void ConnectEvents()
    {
        Navigation.SelectionChanged += (_, _) =>
        {
            OperationsPage.IsVisible = Navigation.SelectedIndex == 0;
            SettingsPage.IsVisible = Navigation.SelectedIndex == 1;
            ServicesPage.IsVisible = Navigation.SelectedIndex == 2;
            if (Navigation.SelectedIndex == 2) RefreshServices();
        };
        ConfigureButton.Click += (_, _) => Navigation.SelectedIndex = 1;
        OpenXmlButton.Click += async (_, _) => await RunAsync("Carregar XML", LoadDocumentsAsync);
        ValidateButton.Click += async (_, _) => await RunAsync("Validar XML", ValidateDocumentsAsync);
        SignButton.Click += async (_, _) => await RunAsync("Assinar XML", SignDocumentsAsync);
        SaveXmlButton.Click += async (_, _) => await RunAsync("Salvar XMLs", SaveDocumentsAsync);
        SendButton.Click += async (_, _) => await RunAsync("Enviar documento", SendDocumentsAsync);
        StatusButton.Click += async (_, _) => await RunAsync("Status do serviço", async token =>
            ShowResponse(await Client().ConsultarSituacaoServicoAsync(token)));
        QueryButton.Click += async (_, _) => await RunAsync("Consultar NF-e", async token =>
            ShowResponse(await Client().ConsultarAsync(AccessKey(), token)));
        ReceiptButton.Click += async (_, _) => await RunAsync("Consultar recibo", ReceiptAsync);
        RegistrationButton.Click += async (_, _) => await RunAsync("Cadastro SEFAZ", RegistrationAsync);
        DistributionButton.Click += async (_, _) => await RunAsync("Distribuição de documentos", DistributionAsync);
        CancelDocumentButton.Click += async (_, _) => await RunAsync("Cancelar documento", CancelDocumentAsync);
        CorrectionButton.Click += async (_, _) => await RunAsync("Carta de correção", CorrectionAsync);
        VoidButton.Click += async (_, _) => await RunAsync("Inutilizar numeração", VoidNumbersAsync);
        PdfButton.Click += async (_, _) => await RunAsync("Gerar DANFE PDF", ExportPdfAsync);
        EventPdfButton.Click += async (_, _) => await RunAsync("Gerar PDF de evento ou inutilização", ExportEventPdfAsync);
        PreviewButton.Click += async (_, _) => await RunAsync("Prévia NFC-e", PreviewAsync);
        EscPosButton.Click += async (_, _) => await RunAsync("Exportar ESC/POS", ExportEscPosAsync);
        CancelOperationButton.Click += (_, _) => operation?.Cancel();
        CopyButton.Click += async (_, _) => await RunAsync("Copiar retorno", async _ =>
        {
            TextBox?[] editors = [ResultText, SentXmlText, ReceivedXmlText, SoapText, null, LogText];
            var editor = editors.ElementAtOrDefault(ResponseTabs.SelectedIndex);
            if (Clipboard != null && editor != null) await Clipboard.SetTextAsync(editor.Text ?? "");
        });
        ClearButton.Click += (_, _) =>
        {
            ResultText.Text = SentXmlText.Text = ReceivedXmlText.Text = SoapText.Text = LogText.Text = "";
            LastOperationText.Text = "";
            ReceiptPreview.Source = null;
            preview?.Dispose();
            preview = null;
        };
        Closing += (_, args) =>
        {
            if (operation == null) return;
            args.Cancel = true;
            closeAfterOperation = true;
            operation.Cancel();
        };
        Closed += (_, _) => preview?.Dispose();
        ConnectSettingsEvents();
    }

    private IOpenNFeClient Client()
    {
        CreateConfiguration();
        return client;
    }

    private async Task RunAsync(string name, Func<CancellationToken, Task> action)
    {
        if (operation != null) return;
        using var cancellation = new CancellationTokenSource();
        operation = cancellation;
        Workspace.IsEnabled = Navigation.IsEnabled = false;
        BusyProgress.IsVisible = CancelOperationButton.IsVisible = true;
        StatusText.Text = name + "…";
        AppendLog(name);
        try
        {
            await action(cancellation.Token);
            StatusText.Text = "Pronto para operar";
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            StatusText.Text = "Operação cancelada";
            AppendLog("Operação cancelada pelo usuário.");
        }
        catch (Exception exception)
        {
            StatusText.Text = "Não foi possível concluir a operação";
            ResultText.Text = exception.Message;
            ResponseTabs.SelectedIndex = 0;
            AppendLog(exception.Message);
        }
        finally
        {
            LastOperationText.Text = $"{name} · {DateTime.Now:HH:mm:ss}";
            Workspace.IsEnabled = Navigation.IsEnabled = true;
            BusyProgress.IsVisible = CancelOperationButton.IsVisible = false;
            operation = null;
            if (closeAfterOperation) Close();
        }
    }

    private void ShowResponse<T>(NFeResposta<T> response) where T : NFeResultBase<T>
    {
        SentXmlText.Text = PrettyXml(response.XmlEnvio);
        ReceivedXmlText.Text = PrettyXml(response.XmlRetorno);
        SoapText.Text = $"ENVIO\n\n{PrettyXml(response.EnvelopeEnvio)}\n\nRETORNO\n\n{PrettyXml(response.EnvelopeRetorno)}";
        ResultText.Text = $"{response.CStat} · {response.XMotivo}\n\n{PrettyXml(response.XmlRetorno)}";
        AppendLog($"SEFAZ: {response.CStat} · {response.XMotivo}");
        ResponseTabs.SelectedIndex = 0;
    }

    private static string PrettyXml(string? xml)
    {
        if (string.IsNullOrWhiteSpace(xml)) return "";
        try { return XDocument.Parse(xml).ToString(); }
        catch (System.Xml.XmlException) { return xml; }
    }

    private void AppendLog(string message)
        => LogText.Text = string.Join('\n', (LogText.Text + $"{DateTime.Now:HH:mm:ss}  {message}\n").Split('\n').TakeLast(500));

    private async Task<IStorageFile?> PickFileAsync(string title, string name, params string[] patterns)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = title, FileTypeFilter = [new FilePickerFileType(name) { Patterns = patterns }]
        });
        return files.FirstOrDefault();
    }

    private async Task<IStorageFile?> PickOutputAsync(string title, string name, string extension)
        => await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = title, SuggestedFileName = name, DefaultExtension = extension,
            FileTypeChoices = [new FilePickerFileType(extension.ToUpperInvariant()) { Patterns = ["*." + extension] }]
        });

    private static async Task WriteAsync(IStorageFile destination, byte[] bytes, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        await using var output = await destination.OpenWriteAsync();
        output.SetLength(0);
        await output.WriteAsync(bytes, token);
    }
}

public sealed record ServiceEndpoint(string Name, string Address);
