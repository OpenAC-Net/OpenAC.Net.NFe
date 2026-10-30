using OpenAC.Net.DFe.Core.Attributes;
using OpenAC.Net.DFe.Core.Document;
using OpenAC.Net.DFe.Core.Serializer;

namespace OpenAC.Net.NFe.Common.Model;

[DFeRoot("consNSU", Namespace = NFeConst.Namespace)]
public sealed partial class NFeConsNSU : DFeDocument<NFeConsNSU>
{
    [DFeElement(TipoCampo.Str, "NSU", Ordem = 10, Ocorrencia = Ocorrencia.NaoObrigatoria)]
    public string NSU { get; set; }
}
