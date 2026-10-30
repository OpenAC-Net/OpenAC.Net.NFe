using OpenAC.Net.DFe.Core.Attributes;
using OpenAC.Net.DFe.Core.Collection;
using OpenAC.Net.DFe.Core.Serializer;

namespace OpenAC.Net.NFe.Common.Model;

[DFeRoot("retConsSitNFe", Namespace = NFeConst.Namespace)]
public sealed partial class NFeConsultaResult : NFeResultBase<NFeConsultaResult>
{
    [DFeElement(TipoCampo.Str, "chNFe", Ordem = 110, Ocorrencia = Ocorrencia.NaoObrigatoria)]
    public string Chave { get; set; }

    [DFeElement("protNFe", Ordem = 120, Ocorrencia = Ocorrencia.NaoObrigatoria)]
    public NFeProtNFe Protocolo { get; set; }

    [DFeCollection("procEventoNFe", Ordem = 130, Ocorrencia = Ocorrencia.NaoObrigatoria)]
    public DFeCollection<NFeProcEvento> Eventos { get; set; } = new();
}
