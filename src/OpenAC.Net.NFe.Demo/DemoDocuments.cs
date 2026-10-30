using System.Xml.Linq;
using OpenAC.Net.DFe.Core.Extensions;
using OpenAC.Net.NFe.Common;
using OpenAC.Net.NFe.Common.Model;
using OpenAC.Net.NFe.Common.Types;
using NotaFiscal = OpenAC.Net.NFe.Common.Model.NFe;

namespace OpenAC.Net.NFe.Demo;

internal sealed class DemoDocuments
{
    internal List<NFeProc> Items { get; } = [];
    internal IReadOnlyList<NotaFiscal> Sent { get; set; } = [];
    internal string Receipt { get; set; } = "";

    internal static NFeProc Parse(string xml)
    {
        var root = XElement.Parse(xml);
        if (root.Name == XName.Get("nfeProc", NFeConst.Namespace))
        {
            var processed = NFeProc.ReadFromXml(root) ?? throw new ArgumentException("Documento processado inválido.");
            if (processed.NFe?.InfNFe?.Ide == null) throw new ArgumentException("NF-e ausente do documento processado.");
            return processed;
        }
        if (root.Name == XName.Get("NFe", NFeConst.Namespace))
        {
            var note = NotaFiscal.ReadFromXml(root) ?? throw new ArgumentException("Documento NF-e inválido.");
            if (note.InfNFe?.Ide == null) throw new ArgumentException("Identificação da NF-e ausente.");
            return new NFeProc { NFe = note, Versao = note.InfNFe.Versao };
        }
        throw new ArgumentException("Esperado XML NFe ou nfeProc no namespace oficial.");
    }

    internal void Replace(IEnumerable<string> xmls)
    {
        var loaded = xmls.Select(Parse).ToArray();
        Items.Clear();
        Items.AddRange(loaded);
        Sent = [];
        Receipt = "";
    }

    internal void Require()
    {
        if (Items.Count == 0) throw new InvalidOperationException("Carregue um XML de NF-e ou NFC-e para continuar.");
    }

    internal void RequireMatching(NFeConfig config)
    {
        Require();
        foreach (var item in Items)
        {
            var ide = item.NFe.InfNFe.Ide;
            if (item.Modelo != config.Geral.Modelo || ide.CUF.ToString() != config.WebServices.UF.ToString()
                || ide.TpAmb != config.WebServices.Ambiente || ide.TpEmis.GetDFeValue() != config.Geral.FormaEmissao.GetDFeValue())
                throw new InvalidOperationException("Modelo, UF, ambiente e emissão dos XMLs devem corresponder às configurações. Ajuste as configurações antes de continuar.");
        }
    }

    internal void RequireSendable(NFeConfig config, bool synchronous)
    {
        RequireMatching(config);
        if (Items.Any(item => item.Versao != NFeVersao.v400))
            throw new InvalidOperationException("A comunicação com a SEFAZ aceita somente documentos 4.00. A versão 3.10 pode ser lida, validada e impressa localmente.");
        if (Items.Any(item => item.ProtNFe != null))
            throw new InvalidOperationException("O lote contém documentos já processados. Carregue apenas NF-e sem protocolo para enviar.");
        if (synchronous && Items.Count != 1 || !synchronous && (Items.Count > 50 || config.Geral.Modelo == ModeloNFe.NFCe))
            throw new InvalidOperationException("Use um documento no envio síncrono; o assíncrono aceita até 50 NF-e modelo 55.");
    }

    internal void Merge(IEnumerable<NFeProc> authorized)
    {
        foreach (var item in authorized)
        {
            var index = Items.FindIndex(existing => existing.Chave == item.Chave);
            if (index >= 0) Items[index] = item;
            else Items.Add(item);
        }
    }

    internal static string Xml(NFeProc item) => item.ProtNFe == null ? item.NFe.GetXml() : item.GetXml();
}
