using OpenAC.Net.DFe.Core.Attributes;
using OpenAC.Net.DFe.Core.Document;
using OpenAC.Net.DFe.Core.Serializer;

namespace OpenAC.Net.NFe.Common.Model;

[DFeRoot("docZip", Namespace = NFeConst.Namespace)]
public sealed partial class NFeDocZip : DFeDocument<NFeDocZip>
{
    [DFeAttribute(TipoCampo.Str, "NSU", Ocorrencia = Ocorrencia.Obrigatoria)]
    public string NSU { get; set; }

    [DFeAttribute(TipoCampo.Str, "schema", Ocorrencia = Ocorrencia.Obrigatoria)]
    public string Schema { get; set; }

    [DFeItemValue(TipoCampo.Str)]
    public string Conteudo { get; set; }
}
