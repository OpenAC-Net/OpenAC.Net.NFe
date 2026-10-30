using OpenAC.Net.DFe.Core.Attributes;
using OpenAC.Net.DFe.Core.Document;
using OpenAC.Net.DFe.Core.Serializer;

namespace OpenAC.Net.NFe.Common.Model;

[DFeRoot("consReciNFe", Namespace = NFeConst.Namespace)]
public sealed partial class NFeConsultaRecibo : DFeDocument<NFeConsultaRecibo>
{
    [DFeAttribute(TipoCampo.Str, "versao", Ocorrencia = Ocorrencia.Obrigatoria)]
    public string Versao { get; set; } = "4.00";

    [DFeElement(TipoCampo.Str, "tpAmb", Ordem = 20, Ocorrencia = Ocorrencia.NaoObrigatoria)]
    public string TpAmb { get; set; }

    [DFeElement(TipoCampo.Str, "nRec", Ordem = 30, Ocorrencia = Ocorrencia.NaoObrigatoria)]
    public string Recibo { get; set; }
}
