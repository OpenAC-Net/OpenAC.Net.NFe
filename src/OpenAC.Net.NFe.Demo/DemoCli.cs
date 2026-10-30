using System.Globalization;
using System.Xml.Linq;
using OpenAC.Net.Devices;
using OpenAC.Net.EscPos;
using OpenAC.Net.NFe.Common;
using OpenAC.Net.NFe.Common.Model;
using OpenAC.Net.NFe.DANFe.EscPos;
using OpenAC.Net.NFe.DANFe.PDFSharp;

namespace OpenAC.Net.NFe.Demo;

internal static class DemoCli
{
    public static int Run(string[] args)
    {
        if (args.Length < 3)
        {
            Console.WriteLine("OpenAC.Net.NFe - impressão local de XML NF-e/NFC-e 3.10/4.00");
            Console.WriteLine("  danfe <nota.xml> <saida.pdf> [58|80]");
            Console.WriteLine("  evento <evento.xml> <saida.pdf>");
            Console.WriteLine("  inutilizacao <processada.xml> <saida.pdf>");
            Console.WriteLine("  escpos <nfce.xml> <saida.bin> [32|48]");
            Console.WriteLine("  escpos-preview <nfce.xml> <saida.png> [32|48]");
            Console.WriteLine("Execute sem argumentos para abrir a interface Avalonia.");
            return args.Length == 1 && args[0] is "--help" or "-h" ? 0 : 1;
        }
        try
        {
            switch (args[0].ToLowerInvariant())
            {
                case "danfe":
                    var pdf = new OpenDANFe();
                    if (args.Length > 3) pdf.Configuracoes.LarguraNFCeMilimetros = int.Parse(args[3], CultureInfo.InvariantCulture);
                    pdf.GerarPDF(args[2], LoadNote(args[1]));
                    break;
                case "evento":
                    File.WriteAllBytes(args[2], new OpenDANFe().GerarEventoPDF(LoadEvent(args[1])));
                    break;
                case "inutilizacao":
                    File.WriteAllBytes(args[2], new OpenDANFe().GerarInutilizacaoPDF(NFeProcInutilizacao.Load(args[1])));
                    break;
                case "escpos":
                    var danfe = new OpenDANFeEscPos();
                    if (args.Length > 3) danfe.Configuracoes.Colunas = int.Parse(args[3], CultureInfo.InvariantCulture);
                    var note = LoadNote(args[1]);
                    File.WriteAllBytes(args[2], []);
                    using (var printer = new EscPosPrinter<FileConfig>(new FileConfig(args[2])))
                    {
                        printer.Colunas = danfe.Configuracoes.Colunas ?? 48;
                        printer.Conectar();
                        danfe.Imprimir(note, printer);
                    }
                    break;
                case "escpos-preview":
                    var preview = new OpenDANFeEscPos();
                    if (args.Length > 3) preview.Configuracoes.Colunas = int.Parse(args[3], CultureInfo.InvariantCulture);
                    File.WriteAllBytes(args[2], preview.GerarPreview(LoadNote(args[1])));
                    break;
                default:
                    throw new ArgumentException("Comando desconhecido: " + args[0]);
            }
            Console.WriteLine("Arquivo gerado: " + Path.GetFullPath(args[2]));
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception.Message);
            return 1;
        }
    }

    private static NFeProc LoadNote(string path) => DemoDocuments.Parse(File.ReadAllText(path));

    private static NFeProcEvento LoadEvent(string path)
    {
        var root = XElement.Load(path);
        XNamespace ns = NFeConst.Namespace;
        if (root.Name == ns + "procEventoNFe")
            return NFeProcEvento.ReadFromXml(root) ?? throw new ArgumentException("Evento processado inválido.");
        if (root.Name == ns + "evento")
            return new NFeProcEvento { Evento = NFeEvento.ReadFromXml(root) };
        throw new ArgumentException("Esperado evento ou procEventoNFe no namespace oficial.");
    }
}
