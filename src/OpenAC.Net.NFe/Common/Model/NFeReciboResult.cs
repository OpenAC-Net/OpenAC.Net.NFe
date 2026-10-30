using OpenAC.Net.DFe.Core.Attributes;
using OpenAC.Net.DFe.Core.Collection;
using OpenAC.Net.DFe.Core.Serializer;

namespace OpenAC.Net.NFe.Common.Model;

[DFeRoot("retConsReciNFe", Namespace = NFeConst.Namespace)]
public sealed partial class NFeReciboResult : NFeResultBase<NFeReciboResult>
{
    [DFeElement(TipoCampo.Str, "nRec", Ordem = 25, Ocorrencia = Ocorrencia.NaoObrigatoria)]
    public string Recibo { get; set; }

    [DFeCollection("protNFe", Ordem = 120, Ocorrencia = Ocorrencia.NaoObrigatoria)]
    public DFeCollection<NFeProtNFe> Protocolos { get; set; } = new();
}
