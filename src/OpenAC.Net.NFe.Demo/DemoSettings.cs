using System.Text.Json;
using OpenAC.Net.DFe.Core.Common;
using OpenAC.Net.NFe.Common.Types;

namespace OpenAC.Net.NFe.Demo;

// Senha do certificado e CSC pertencem apenas à sessão, sem persistência em disco.
internal sealed class DemoSettings
{
    public ModeloNFe Model { get; set; } = ModeloNFe.NFe;
    public DFeTipoAmbiente Environment { get; set; } = DFeTipoAmbiente.Homologacao;
    public DFeSiglaUF State { get; set; } = DFeSiglaUF.SP;
    public DFeTipoEmissao Emission { get; set; } = DFeTipoEmissao.Normal;
    public string CertificatePath { get; set; } = "";
    public string CertificateSerial { get; set; } = "";
    public string SchemasPath { get; set; } = Path.Combine(AppContext.BaseDirectory, "Schemas");
    public string DocumentsPath { get; set; } = Path.Combine(Home, "Documentos");
    public string CatalogPath { get; set; } = "";
    public bool SaveDocuments { get; set; }
    public bool SaveSoap { get; set; }
    public bool OpenPdf { get; set; } = true;
    public bool Landscape { get; set; }
    public bool Synchronous { get; set; } = true;
    public int ReceiptWidth { get; set; } = 80;
    public int Columns { get; set; } = 48;
    public int TimeoutSeconds { get; set; } = 60;
    public string Batch { get; set; } = "1";
    public string CscId { get; set; } = "";
    public NFeVersaoQRCode QrVersion { get; set; } = NFeVersaoQRCode.v300;

    internal static string Home => Path.Combine(System.Environment.GetFolderPath(
        System.Environment.SpecialFolder.LocalApplicationData), "OpenAC.Net", "NFe.Demo");

    internal static DemoSettings Load(string path) => File.Exists(path)
        ? JsonSerializer.Deserialize<DemoSettings>(File.ReadAllText(path)) ?? new DemoSettings()
        : new DemoSettings();

    internal void Save(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var temporary = path + ".tmp";
        try
        {
            File.WriteAllText(temporary, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
            File.Move(temporary, path, true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
