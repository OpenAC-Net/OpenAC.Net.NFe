using OpenAC.Net.DFe.Core.Attributes;
using OpenAC.Net.DFe.Core.Document;
using OpenAC.Net.DFe.Core.Serializer;

namespace OpenAC.Net.NFe.Common.Model;

[DFeRoot("procEventoNFe", Namespace = NFeConst.Namespace)]
public sealed partial class NFeProcEvento : DFeDocument<NFeProcEvento>
{
    [DFeAttribute(TipoCampo.Str, "versao", Ocorrencia = Ocorrencia.Obrigatoria)]
    public string Versao { get; set; } = "1.00";

    [DFeElement("evento", Ordem = 20, Ocorrencia = Ocorrencia.NaoObrigatoria)]
    public NFeEvento Evento { get; set; }

    [DFeElement("retEvento", Ordem = 30, Ocorrencia = Ocorrencia.NaoObrigatoria)]
    public NFeRetEvento RetEvento { get; set; }
}
