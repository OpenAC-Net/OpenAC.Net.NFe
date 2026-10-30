using OpenAC.Net.DFe.Core.Attributes;
using OpenAC.Net.DFe.Core.Collection;
using OpenAC.Net.DFe.Core.Document;
using OpenAC.Net.DFe.Core.Serializer;

namespace OpenAC.Net.NFe.Common.Model;

[DFeRoot("envEvento", Namespace = NFeConst.Namespace)]
public sealed partial class NFeEnvioEvento : DFeDocument<NFeEnvioEvento>
{
    [DFeAttribute(TipoCampo.Str, "versao", Ocorrencia = Ocorrencia.Obrigatoria)]
    public string Versao { get; set; } = "1.00";

    [DFeElement(TipoCampo.Str, "idLote", Ordem = 20, Ocorrencia = Ocorrencia.NaoObrigatoria)]
    public string Lote { get; set; }

    [DFeCollection("evento", Ordem = 30, Ocorrencia = Ocorrencia.NaoObrigatoria)]
    public DFeCollection<NFeEvento> Eventos { get; set; } = new();
}
