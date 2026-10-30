using OpenAC.Net.DFe.Core.Attributes;
using OpenAC.Net.DFe.Core.Collection;
using OpenAC.Net.DFe.Core.Serializer;

namespace OpenAC.Net.NFe.Common.Model;

[DFeRoot("retEnvEvento", Namespace = NFeConst.Namespace)]
public sealed partial class NFeEventoResult : NFeResultBase<NFeEventoResult>
{
    [DFeElement(TipoCampo.Str, "idLote", Ordem = 5, Ocorrencia = Ocorrencia.NaoObrigatoria)]
    public string Lote { get; set; }

    [DFeElement(TipoCampo.Str, "cOrgao", Ordem = 25, Ocorrencia = Ocorrencia.NaoObrigatoria)]
    public string Orgao { get; set; }

    [DFeCollection("retEvento", Ordem = 120, Ocorrencia = Ocorrencia.NaoObrigatoria)]
    public DFeCollection<NFeRetEvento> Eventos { get; set; } = new();
}
