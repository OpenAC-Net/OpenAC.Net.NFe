using OpenAC.Net.DFe.Core.Attributes;
using OpenAC.Net.DFe.Core.Document;
using OpenAC.Net.DFe.Core.Serializer;
using OpenAC.Net.NFe.Common.Types;

namespace OpenAC.Net.NFe.Common.Model;

[DFeSignInfoElement("infProt")]
[DFeRoot("protNFe", Namespace = NFeConst.Namespace)]
public sealed partial class NFeProtNFe : DFeSignDocument<NFeProtNFe>
{
    [DFeAttribute(TipoCampo.Enum, "versao", Ocorrencia = Ocorrencia.Obrigatoria)]
    public NFeVersao Versao { get; set; } = NFeVersao.v400;

    [DFeElement("infProt", Ordem = 20, Ocorrencia = Ocorrencia.Obrigatoria)]
    public NFeInfProt InfProt { get; set; }

    protected override bool ShouldSerializeSignature() => Signature != null && base.ShouldSerializeSignature();
}
