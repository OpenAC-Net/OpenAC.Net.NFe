using System.IO;
using OpenAC.Net.DFe.Core;
using OpenAC.Net.DFe.Core.Attributes;
using OpenAC.Net.DFe.Core.Document;
using OpenAC.Net.DFe.Core.Extensions;
using OpenAC.Net.DFe.Core.Serializer;
using OpenAC.Net.NFe.Common.Types;

namespace OpenAC.Net.NFe.Common.Model;

/// <summary>NF-e processada, contendo a nota e o protocolo retornado pela SEFAZ.</summary>
[DFeRoot("nfeProc", Namespace = NFeConst.Namespace)]
public sealed partial class NFeProc : DFeDocument<NFeProc>
{
    [DFeAttribute(TipoCampo.Enum, "versao", Ocorrencia = Ocorrencia.Obrigatoria)]
    public NFeVersao Versao { get; set; } = NFeVersao.v400;

    [DFeElement("NFe", Ordem = 10, Ocorrencia = Ocorrencia.Obrigatoria)]
    public NFe NFe { get; set; }

    [DFeElement("protNFe", Ordem = 20, Ocorrencia = Ocorrencia.Obrigatoria)]
    public NFeProtNFe ProtNFe { get; set; }

    [DFeIgnore]
    public bool Autorizado => ProtNFe?.InfProt?.CStat is 100 or 150;

    /// <summary>Situação obtida por consulta/evento, sem alterar os dados fiscais assinados.</summary>
    [DFeIgnore]
    public bool Cancelado { get; set; }

    [DFeIgnore]
    public string Chave => NFe?.Chave ?? "";

    [DFeIgnore]
    public ModeloNFe Modelo => NFe?.Modelo ?? 0;

    [DFeIgnore]
    public string QrCode => NFe?.QrCode ?? "";

    public void ValidarProtocolo()
    {
        if (NFe?.InfNFe == null || ProtNFe?.InfProt == null || Versao != NFe.InfNFe.Versao ||
            ProtNFe.Versao != Versao || ProtNFe.InfProt.ChNFe != Chave)
            throw new OpenDFeValidationException("Protocolo não corresponde à chave/versão da NF-e.");
        var digest = NFe.Signature?.SignedInfo?.Reference?.DigestValue;
        if (!string.IsNullOrEmpty(digest) && digest != ProtNFe.InfProt.DigVal)
            throw new OpenDFeValidationException("DigestValue do protocolo difere da assinatura da NF-e.");
    }

    public bool ValidarAssinatura() => NFe?.ValidarAssinatura() == true;

    public void Validar(string pathSchemas)
    {
        ValidarProtocolo();
        NFeValidacao.Schema(GetXml(), Path.Combine(pathSchemas, "procNFe_v" + Versao.GetDFeValue() + ".xsd"));
    }
}
