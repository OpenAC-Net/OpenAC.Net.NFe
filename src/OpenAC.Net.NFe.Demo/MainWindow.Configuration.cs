using System.Text;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using OpenAC.Net.DFe.Core.Common;
using OpenAC.Net.NFe.Common;
using OpenAC.Net.NFe.Common.Types;
using OpenAC.Net.NFe.Services;

namespace OpenAC.Net.NFe.Demo;

public partial class MainWindow
{
    private void InitializeSettings()
    {
        try { settings = DemoSettings.Load(configurationPath); }
        catch (Exception exception) when (exception is IOException or System.Text.Json.JsonException or UnauthorizedAccessException)
        {
            ResultText.Text = "Não foi possível ler as configurações. Os valores padrão foram carregados.\n\n" + exception.Message;
        }
        Fill(ModelCombo, new[] { ModeloNFe.NFe, ModeloNFe.NFCe }, settings.Model, value => value == ModeloNFe.NFe ? "NF-e · 55" : "NFC-e · 65");
        Fill(EnvironmentCombo, new[] { DFeTipoAmbiente.Homologacao, DFeTipoAmbiente.Producao }, settings.Environment, value => value == DFeTipoAmbiente.Homologacao ? "Homologação" : "Produção");
        Fill(StateCombo, Enum.GetValues<DFeSiglaUF>().Where(value => value is not (DFeSiglaUF.AN or DFeSiglaUF.EX or DFeSiglaUF.SU)).OrderBy(value => value.ToString()), settings.State);
        FillEmission(settings.Emission);
        Fill(PdfOrientationCombo, new[] { false, true }, settings.Landscape, value => value ? "Paisagem" : "Retrato");
        Fill(ReceiptWidthCombo, new[] { 80, 58 }, settings.ReceiptWidth, value => $"{value} mm");
        Fill(ColumnsCombo, new[] { 48, 32 }, settings.Columns, value => $"{value} colunas");
        Fill(QrVersionCombo, new[] { NFeVersaoQRCode.v300, NFeVersaoQRCode.v200 }, settings.QrVersion, value => value == NFeVersaoQRCode.v300 ? "3.00" : "2.00");
        CertificatePathText.Text = settings.CertificatePath;
        CertificateSerialText.Text = settings.CertificateSerial;
        SchemasPathText.Text = settings.SchemasPath;
        DocumentsPathText.Text = settings.DocumentsPath;
        SaveDocumentsCheck.IsChecked = settings.SaveDocuments;
        SaveSoapCheck.IsChecked = settings.SaveSoap;
        OpenPdfCheck.IsChecked = settings.OpenPdf;
        SynchronousCheck.IsChecked = settings.Synchronous;
        TimeoutInput.Value = Math.Clamp(settings.TimeoutSeconds, 1, 600);
        BatchText.Text = settings.Batch;
        CscIdText.Text = settings.CscId;
        ConfigurationPathText.Text = "Configurações: " + configurationPath + "\nSenha e CSC não são gravados.";
    }

    private void FillEmission(DFeTipoEmissao selected)
    {
        var values = Selected<ModeloNFe>(ModelCombo) == ModeloNFe.NFCe
            ? new[] { DFeTipoEmissao.Normal, DFeTipoEmissao.OffLine }
            : new[] { DFeTipoEmissao.Normal, DFeTipoEmissao.Contingencia, DFeTipoEmissao.DPEC, DFeTipoEmissao.FSDA, DFeTipoEmissao.SVCAN, DFeTipoEmissao.SVCRS };
        Fill(EmissionCombo, values, selected);
    }

    private void ConnectSettingsEvents()
    {
        ModelCombo.SelectionChanged += (_, _) =>
        {
            if (!initialized) return;
            var emission = Selected<DFeTipoEmissao>(EmissionCombo);
            initialized = false;
            try { FillEmission(emission); }
            finally { initialized = true; }
            if (Selected<ModeloNFe>(ModelCombo) == ModeloNFe.NFCe) SynchronousCheck.IsChecked = true;
            UpdateContext();
            RefreshServices();
        };
        foreach (var combo in new[] { EnvironmentCombo, StateCombo, EmissionCombo })
            combo.SelectionChanged += (_, _) => { if (initialized) { UpdateContext(); RefreshServices(); } };
        BrowseCertificateButton.Click += async (_, _) => await RunAsync("Selecionar certificado", async _ =>
        {
            var file = await PickFileAsync("Selecionar certificado digital", "Certificado PFX/P12", "*.pfx", "*.p12");
            if (file != null) CertificatePathText.Text = file.TryGetLocalPath() ?? throw new InvalidOperationException("Selecione um arquivo local.");
        });
        BrowseSchemasButton.Click += async (_, _) => await SelectFolderAsync(SchemasPathText);
        BrowseDocumentsButton.Click += async (_, _) => await SelectFolderAsync(DocumentsPathText);
        SaveSettingsButton.Click += async (_, _) => await RunAsync("Salvar configurações", _ =>
        {
            ReadSettings().Save(configurationPath);
            ResultText.Text = "Configurações salvas em " + configurationPath;
            ResponseTabs.SelectedIndex = 0;
            return Task.CompletedTask;
        });
        RefreshServicesButton.Click += (_, _) => RefreshServices();
        LoadServicesButton.Click += async (_, _) => await RunAsync("Carregar catálogo", async token =>
        {
            var file = await PickFileAsync("Carregar catálogo de serviços", "Catálogo XML", "*.xml", "*.nfe");
            if (file == null) return;
            await using var input = await file.OpenReadAsync();
            token.ThrowIfCancellationRequested();
            var catalog = NFeServiceManager.CarregarCatalogo(input);
            var path = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(configurationPath))!, "services.nfe.xml");
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            await File.WriteAllTextAsync(path, catalog.GetXml(), token);
            settings.CatalogPath = path;
            RefreshServices();
            ResultText.Text = "Catálogo carregado. Salve as configurações para reutilizá-lo na próxima sessão.";
        });
        ResetServicesButton.Click += (_, _) => { settings.CatalogPath = ""; RefreshServices(); };
        SaveServicesButton.Click += async (_, _) => await RunAsync("Exportar catálogo", async token =>
        {
            var destination = await PickOutputAsync("Exportar catálogo de serviços", "services.nfe.xml", "xml");
            if (destination == null) return;
            using var input = string.IsNullOrWhiteSpace(settings.CatalogPath)
                ? typeof(OpenNFe).Assembly.GetManifestResourceStream("OpenAC.Net.NFe.Resources.services.xml")!
                : File.OpenRead(settings.CatalogPath);
            var catalog = NFeServiceManager.CarregarCatalogo(input);
            await WriteAsync(destination, Encoding.UTF8.GetBytes(catalog.GetXml()), token);
            ResultText.Text = "Catálogo exportado: " + destination.Name;
        });
    }

    private DemoSettings ReadSettings()
    {
        settings.Model = Selected<ModeloNFe>(ModelCombo);
        settings.Environment = Selected<DFeTipoAmbiente>(EnvironmentCombo);
        settings.State = Selected<DFeSiglaUF>(StateCombo);
        settings.Emission = Selected<DFeTipoEmissao>(EmissionCombo);
        settings.CertificatePath = CertificatePathText.Text?.Trim() ?? "";
        settings.CertificateSerial = CertificateSerialText.Text?.Trim() ?? "";
        settings.SchemasPath = SchemasPathText.Text?.Trim() ?? "";
        settings.DocumentsPath = DocumentsPathText.Text?.Trim() ?? "";
        settings.SaveDocuments = SaveDocumentsCheck.IsChecked == true;
        settings.SaveSoap = SaveSoapCheck.IsChecked == true;
        settings.OpenPdf = OpenPdfCheck.IsChecked == true;
        settings.Landscape = Selected<bool>(PdfOrientationCombo);
        settings.ReceiptWidth = Selected<int>(ReceiptWidthCombo);
        settings.Columns = Selected<int>(ColumnsCombo);
        settings.TimeoutSeconds = (int)(TimeoutInput.Value ?? 60);
        settings.Batch = BatchText.Text?.Trim() ?? "1";
        settings.CscId = CscIdText.Text?.Trim() ?? "";
        settings.QrVersion = Selected<NFeVersaoQRCode>(QrVersionCombo);
        settings.Synchronous = SynchronousCheck.IsChecked == true;
        return settings;
    }

    internal NFeConfig CreateConfiguration()
    {
        var current = ReadSettings();
        var config = new NFeConfig();
        config.Geral.Modelo = current.Model;
        config.Geral.FormaEmissao = current.Emission;
        config.Geral.VersaoDFe = NFeVersao.v400;
        config.Geral.VersaoQRCode = current.QrVersion;
        config.Geral.IdCSC = current.CscId;
        config.Geral.CSC = CscText.Text ?? "";
        config.WebServices.UF = current.State;
        config.WebServices.Ambiente = current.Environment;
        config.WebServices.TimeoutOperacao = TimeSpan.FromSeconds(current.TimeoutSeconds);
        config.WebServices.Salvar = current.SaveSoap;
        config.Certificados.Certificado = string.IsNullOrWhiteSpace(current.CertificatePath) ? current.CertificateSerial : current.CertificatePath;
        config.Certificados.Senha = CertificatePasswordText.Text ?? "";
        config.Arquivos.PathSchemas = current.SchemasPath;
        config.Arquivos.PathSalvar = current.DocumentsPath;
        config.Arquivos.Salvar = current.SaveDocuments;
        if (!string.IsNullOrWhiteSpace(current.CatalogPath))
        {
            config.Arquivos.ArquivoServicos = "";
            config.Arquivos.ArquivoServicos = current.CatalogPath;
        }
        else config.Arquivos.ArquivoServicos = "";
        return config;
    }

    private void UpdateContext()
    {
        var model = Selected<ModeloNFe>(ModelCombo);
        var environment = Selected<DFeTipoAmbiente>(EnvironmentCombo);
        EnvironmentText.Text = environment == DFeTipoAmbiente.Homologacao ? "Homologação" : "Produção";
        EnvironmentBadge.Background = Brush.Parse(environment == DFeTipoAmbiente.Producao ? "#FFE8DB" : "#EAF2FF");
        ContextText.Text = $"{(model == ModeloNFe.NFCe ? "NFC-e · 65" : "NF-e · 55")} · 4.00 · {Selected<DFeSiglaUF>(StateCombo)} · {Selected<DFeTipoEmissao>(EmissionCombo)}";
        CorrectionButton.IsEnabled = DistributionButton.IsEnabled = ReceiptButton.IsEnabled = model == ModeloNFe.NFe;
        SynchronousCheck.IsEnabled = model == ModeloNFe.NFe;
        PreviewButton.IsEnabled = EscPosButton.IsEnabled = model == ModeloNFe.NFCe;
    }

    private void RefreshServices()
    {
        if (!initialized) return;
        endpoints.Clear();
        try
        {
            var config = CreateConfiguration();
            foreach (var service in Enum.GetValues<TipoServicoNFe>())
            {
                try { endpoints.Add(new ServiceEndpoint(service.ToString(), NFeServiceManager.ObterEndereco(config, service))); }
                catch (NotSupportedException) { endpoints.Add(new ServiceEndpoint(service.ToString(), "Indisponível nesta configuração")); }
            }
        }
        catch (Exception exception)
        {
            ResultText.Text = "Não foi possível carregar o catálogo: " + exception.Message;
            AppendLog(exception.Message);
        }
    }

    private async Task SelectFolderAsync(TextBox target) => await RunAsync("Selecionar diretório", async _ =>
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = "Selecionar diretório" });
        if (folders.Count > 0) target.Text = folders[0].TryGetLocalPath() ?? throw new InvalidOperationException("Selecione um diretório local.");
    });

    private static void Fill<T>(ComboBox combo, IEnumerable<T> values, T selected, Func<T, string>? label = null)
    {
        var choices = values.Select(value => new Choice<T>(value, label?.Invoke(value) ?? value?.ToString() ?? "")).ToArray();
        combo.ItemsSource = choices;
        combo.SelectedItem = choices.FirstOrDefault(choice => EqualityComparer<T>.Default.Equals(choice.Value, selected)) ?? choices[0];
    }

    private static T Selected<T>(ComboBox combo) => ((Choice<T>)combo.SelectedItem!).Value;
    private sealed record Choice<T>(T Value, string Label) { public override string ToString() => Label; }
}
