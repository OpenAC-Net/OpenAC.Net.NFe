using OpenAC.Net.DFe.Core.Attributes;
using OpenAC.Net.DFe.Core.Serializer;

namespace OpenAC.Net.NFe.Common.Model;

[DFeRoot("retDistDFeInt", Namespace = NFeConst.Namespace)]
public sealed partial class NFeDistribuicaoResult : NFeResultBase<NFeDistribuicaoResult>
{
    [DFeElement(TipoCampo.Str, "dhResp", Ordem = 110, Ocorrencia = Ocorrencia.NaoObrigatoria)]
    public string DataResposta { get; set; }

    [DFeElement(TipoCampo.Str, "ultNSU", Ordem = 120, Ocorrencia = Ocorrencia.NaoObrigatoria)]
    public string UltNSU { get; set; }

    [DFeElement(TipoCampo.Str, "maxNSU", Ordem = 130, Ocorrencia = Ocorrencia.NaoObrigatoria)]
    public string MaxNSU { get; set; }

    [DFeElement("loteDistDFeInt", Ordem = 140, Ocorrencia = Ocorrencia.NaoObrigatoria)]
    public NFeDistribuicaoLote Lote { get; set; }
}
