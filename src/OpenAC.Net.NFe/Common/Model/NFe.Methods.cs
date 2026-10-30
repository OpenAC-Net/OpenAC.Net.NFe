using System;
using System.Globalization;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using OpenAC.Net.DFe.Core;
using OpenAC.Net.DFe.Core.Attributes;
using OpenAC.Net.DFe.Core.Common;
using OpenAC.Net.DFe.Core.Extensions;
using OpenAC.Net.NFe.Common.Types;
using OpenAC.Net.NFe.Services;

namespace OpenAC.Net.NFe.Common.Model;

public sealed partial class NFe
{
    [DFeIgnore]
    public string Chave => InfNFe?.Id?.StartsWith("NFe", StringComparison.Ordinal) == true ? InfNFe.Id.Substring(3) : "";

    [DFeIgnore]
    public ModeloNFe Modelo => int.TryParse((string)InfNFe?.Ide?.Mod, NumberStyles.None, CultureInfo.InvariantCulture, out var modelo) ? (ModeloNFe)modelo : 0;

    [DFeIgnore]
    public bool Assinado => !string.IsNullOrEmpty(Signature?.SignatureValue);

    [DFeIgnore]
    public string QrCode => InfNFeSupl?.QrCode ?? "";

    public void GerarChave()
    {
        if (Assinado) throw new InvalidOperationException("A chave de um documento assinado não pode ser alterada.");
        var chave = CalcularChave();
        InfNFe.Id = "NFe" + chave;
        InfNFe.Ide.CDV = chave.Substring(43);
    }

    private string CalcularChave()
    {
        var ide = InfNFe?.Ide ?? throw new OpenDFeValidationException("ide não informado.");
        var emit = InfNFe.Emit ?? throw new OpenDFeValidationException("Emitente não informado.");
        var documento = string.IsNullOrEmpty(emit.CNPJ) ? (emit.CPF ?? "").PadLeft(14, '0') : emit.CNPJ;
        return NFeChave.Gerar(int.Parse((string)ide.CUF, CultureInfo.InvariantCulture),
            DateTimeOffset.Parse((string)ide.DhEmi, CultureInfo.InvariantCulture), documento, (int)Modelo,
            int.Parse((string)ide.Serie, CultureInfo.InvariantCulture), long.Parse((string)ide.NNF, CultureInfo.InvariantCulture),
            ide.TpEmis, int.Parse((string)ide.CNF, CultureInfo.InvariantCulture));
    }

    public void ValidarIdentificacao()
    {
        NFeValidacao.Chave(Chave);
        if (Chave != CalcularChave() || InfNFe.Ide.CDV != Chave.Substring(43))
            throw new OpenDFeValidationException("A chave de acesso/cDV difere dos campos da NF-e.");
    }

    public void Assinar(X509Certificate2 certificado)
    {
        if (certificado is not { HasPrivateKey: true })
            throw new ArgumentException("Certificado com chave privada obrigatório.", nameof(certificado));
        if (string.IsNullOrEmpty(Chave)) GerarChave();
        ValidarIdentificacao();
        AssinarDocumento(certificado, DFeSaveOptions.DisableFormatting | DFeSaveOptions.OmitDeclaration, false);
    }

    /// <summary>Confere a assinatura dos dados atuais; alterações no modelo invalidam a assinatura.</summary>
    public bool ValidarAssinatura() => Assinado && XmlSigning.ValidarAssinatura<NFe>(this, true);

    protected override bool ShouldSerializeSignature() => Signature != null && base.ShouldSerializeSignature();

    public void Validar(string pathSchemas) => NFeValidacao.Schema(GetXml(),
        Path.Combine(pathSchemas, "nfe_v" + EnumExtensions.GetDFeValue<NFeVersao>(InfNFe.Versao) + ".xsd"));

    public void GerarQrCode(NFeConfig configuracao)
    {
        if (Modelo != ModeloNFe.NFCe || InfNFe.Versao != NFeVersao.v400)
            throw new InvalidOperationException("Geração do QR Code disponível para NFC-e 4.00.");
        if (!Assinado) throw new InvalidOperationException("Assine a NFC-e antes de gerar o QR Code.");
        InfNFeSupl = new NFeSupl
        {
            QrCode = NFCeQrCode.Gerar(this, configuracao),
            UrlChave = NFeServiceManager.UrlConsulta(configuracao)
        };
    }

    public NFeProc ComProtocolo(NFeProtNFe protocolo)
    {
        var documento = new NFeProc
        {
            Versao = InfNFe.Versao,
            NFe = NFeDocumentCopy.Copiar(this),
            ProtNFe = NFeDocumentCopy.Copiar(protocolo)
        };
        documento.ValidarProtocolo();
        return documento;
    }
}
