using OpenAC.Net.DFe.Core.Attributes;
using OpenAC.Net.DFe.Core.Collection;

namespace OpenAC.Net.NFe.Common.Model;

[DFeRoot("retEnviNFe", Namespace = NFeConst.Namespace)]
public sealed partial class NFeAutorizacaoResult : NFeResultBase<NFeAutorizacaoResult>
{
    [DFeElement("infRec", Ordem = 110, Ocorrencia = Ocorrencia.NaoObrigatoria)]
    public NFeReciboInfo InfRec { get; set; }

    [DFeCollection("protNFe", Ordem = 120, Ocorrencia = Ocorrencia.NaoObrigatoria)]
    public DFeCollection<NFeProtNFe> Protocolos { get; set; } = new();
}
