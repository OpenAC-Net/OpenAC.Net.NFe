using OpenAC.Net.DFe.Core.Attributes;
using OpenAC.Net.DFe.Core.Document;
using OpenAC.Net.DFe.Core.Serializer;

namespace OpenAC.Net.NFe.Common.Model;

[DFeSignInfoElement("infInut")]

[DFeRoot("inutNFe", Namespace = NFeConst.Namespace)]
public sealed partial class NFeInutilizacao : DFeSignDocument<NFeInutilizacao>
{
    [DFeAttribute(TipoCampo.Str, "versao", Ocorrencia = Ocorrencia.Obrigatoria)]
    public string Versao { get; set; } = "4.00";

    [DFeElement("infInut", Ordem = 20, Ocorrencia = Ocorrencia.NaoObrigatoria)]
    public NFeInutilizacaoInfo InfInut { get; set; }

    public void Assinar(System.Security.Cryptography.X509Certificates.X509Certificate2 certificado) =>
        AssinarDocumento(certificado, DFe.Core.Common.DFeSaveOptions.DisableFormatting |
            DFe.Core.Common.DFeSaveOptions.OmitDeclaration, false);
}
