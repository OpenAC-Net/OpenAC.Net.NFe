using OpenAC.Net.NFe.Common;
using OpenAC.Net.NFe.Common.Model;
using System.Xml.Linq;
using OpenAC.Net.NFe.DANFe.EscPos;
using OpenAC.Net.NFe.DANFe.PDFSharp;
using OpenAC.Net.Devices;
using OpenAC.Net.EscPos;
using System.Text;

Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

if(args.Length<3)
{
    Console.WriteLine("OpenAC.Net.NFe - impressão local de XML NF-e/NFC-e 3.10/4.00");
    Console.WriteLine("  danfe  <nota.xml>   <saida.pdf> [58|80]");
    Console.WriteLine("  evento <evento.xml> <saida.pdf>");
    Console.WriteLine("  inutilizacao <processada.xml> <saida.pdf>");
    Console.WriteLine("  escpos <nfce.xml>   <saida.bin> [32|48]");
    Console.WriteLine("  escpos-preview <nfce.xml> <saida.png> [32|48]");
    Console.WriteLine("O exemplo grava arquivos; ESC/POS utiliza EscPosPrinter com dispositivo de arquivo.");
    return;
}
try
{
    switch(args[0].ToLowerInvariant())
    {
        case "danfe":
            var pdf=new OpenDANFe();if(args.Length>3)pdf.Configuracoes.LarguraNFCeMilimetros=int.Parse(args[3]);
            pdf.GerarPDF(args[2],CarregarNota(args[1]));break;
        case "evento":File.WriteAllBytes(args[2],new OpenDANFe().GerarEventoPDF(CarregarEvento(args[1])));break;
        case "inutilizacao":File.WriteAllBytes(args[2],new OpenDANFe().GerarInutilizacaoPDF(NFeProcInutilizacao.Load(args[1])));break;
        case "escpos":
            var danfe=new OpenDANFeEscPos();if(args.Length>3)danfe.Configuracoes.Colunas=int.Parse(args[3]);
            var nota=CarregarNota(args[1]);
            File.WriteAllBytes(args[2],[]);
            using(var printer=new EscPosPrinter<FileConfig>(new FileConfig(args[2])))
            {
                printer.Colunas = danfe.Configuracoes.Colunas ?? 48;
                printer.Conectar();
                danfe.Imprimir(nota,printer);
            }
            break;
        case "escpos-preview":
            var previa = new OpenDANFeEscPos();
            if (args.Length > 3) previa.Configuracoes.Colunas = int.Parse(args[3]);
            File.WriteAllBytes(args[2], previa.GerarPreview(CarregarNota(args[1])));
            break;
        default:throw new ArgumentException("Comando desconhecido: "+args[0]);
    }
    Console.WriteLine("Arquivo gerado: "+Path.GetFullPath(args[2]));
}
catch(Exception ex){Console.Error.WriteLine(ex.Message);Environment.ExitCode=1;}

static NFeProc CarregarNota(string arquivo)
{
    var elemento = XElement.Load(arquivo);
    if (elemento.Name == XName.Get("nfeProc", NFeConst.Namespace)) return NFeProc.ReadFromXml(elemento);
    if (elemento.Name == XName.Get("NFe", NFeConst.Namespace))
    {
        var nota = NFe.ReadFromXml(elemento);
        return new NFeProc { NFe = nota, Versao = nota.InfNFe.Versao };
    }
    throw new ArgumentException("Esperado NFe ou nfeProc no namespace oficial.");
}

static NFeProcEvento CarregarEvento(string arquivo)
{
    var elemento = XElement.Load(arquivo);
    if (elemento.Name == XName.Get("procEventoNFe", NFeConst.Namespace)) return NFeProcEvento.ReadFromXml(elemento);
    if (elemento.Name == XName.Get("evento", NFeConst.Namespace)) return new NFeProcEvento { Evento = NFeEvento.ReadFromXml(elemento) };
    throw new ArgumentException("Esperado evento ou procEventoNFe no namespace oficial.");
}
