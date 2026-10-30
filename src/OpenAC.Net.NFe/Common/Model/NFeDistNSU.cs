using OpenAC.Net.DFe.Core.Attributes;
using OpenAC.Net.DFe.Core.Document;
using OpenAC.Net.DFe.Core.Serializer;

namespace OpenAC.Net.NFe.Common.Model;

[DFeRoot("distNSU", Namespace = NFeConst.Namespace)]
public sealed partial class NFeDistNSU : DFeDocument<NFeDistNSU>
{
    [DFeElement(TipoCampo.Str, "ultNSU", Ordem = 10, Ocorrencia = Ocorrencia.NaoObrigatoria)]
    public string UltNSU { get; set; }
}
