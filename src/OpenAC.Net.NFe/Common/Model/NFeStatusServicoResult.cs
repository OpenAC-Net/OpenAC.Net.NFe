using OpenAC.Net.DFe.Core.Attributes;
using OpenAC.Net.DFe.Core.Serializer;

namespace OpenAC.Net.NFe.Common.Model;

[DFeRoot("retConsStatServ", Namespace = NFeConst.Namespace)]
public sealed partial class NFeStatusServicoResult : NFeResultBase<NFeStatusServicoResult>
{
    [DFeElement(TipoCampo.Str, "tMed", Ordem = 110, Ocorrencia = Ocorrencia.NaoObrigatoria)]
    public string TempoMedio { get; set; }

    [DFeElement(TipoCampo.Str, "dhRetorno", Ordem = 120, Ocorrencia = Ocorrencia.NaoObrigatoria)]
    public string DhRetorno { get; set; }

    [DFeElement(TipoCampo.Str, "xObs", Ordem = 130, Ocorrencia = Ocorrencia.NaoObrigatoria)]
    public string Observacao { get; set; }
}
