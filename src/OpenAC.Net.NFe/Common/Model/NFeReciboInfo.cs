using OpenAC.Net.DFe.Core.Attributes;
using OpenAC.Net.DFe.Core.Document;
using OpenAC.Net.DFe.Core.Serializer;

namespace OpenAC.Net.NFe.Common.Model;

[DFeRoot("infRec", Namespace = NFeConst.Namespace)]
public sealed partial class NFeReciboInfo : DFeDocument<NFeReciboInfo>
{
    [DFeElement(TipoCampo.Str, "nRec", Ordem = 10, Ocorrencia = Ocorrencia.NaoObrigatoria)]
    public string Recibo { get; set; }

    [DFeElement(TipoCampo.Str, "tMed", Ordem = 20, Ocorrencia = Ocorrencia.NaoObrigatoria)]
    public string TempoMedio { get; set; }
}
