using OpenAC.Net.DFe.Core.Attributes;
using OpenAC.Net.DFe.Core.Document;
using OpenAC.Net.DFe.Core.Serializer;

namespace OpenAC.Net.NFe.Common.Model;

[DFeSignInfoElement("infEvento")]
[DFeRoot("retEvento", Namespace = NFeConst.Namespace)]
public sealed partial class NFeRetEvento : DFeSignDocument<NFeRetEvento>
{
    [DFeAttribute(TipoCampo.Str, "versao", Ocorrencia = Ocorrencia.Obrigatoria)]
    public string Versao { get; set; } = "1.00";

    [DFeElement("infEvento", Ordem = 20, Ocorrencia = Ocorrencia.Obrigatoria)]
    public NFeRetEventoInfo InfEvento { get; set; }

    protected override bool ShouldSerializeSignature() => Signature != null && base.ShouldSerializeSignature();
}
