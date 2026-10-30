using OpenAC.Net.DFe.Core.Attributes;
using OpenAC.Net.DFe.Core.Document;
using OpenAC.Net.DFe.Core.Serializer;

namespace OpenAC.Net.NFe.Common.Model;

[DFeRoot("consStatServ", Namespace = NFeConst.Namespace)]
public sealed partial class NFeConsultaStatus : DFeDocument<NFeConsultaStatus>
{
    [DFeAttribute(TipoCampo.Str, "versao", Ocorrencia = Ocorrencia.Obrigatoria)]
    public string Versao { get; set; } = "4.00";

    [DFeElement(TipoCampo.Str, "tpAmb", Ordem = 20, Ocorrencia = Ocorrencia.NaoObrigatoria)]
    public string TpAmb { get; set; }

    [DFeElement(TipoCampo.Str, "cUF", Ordem = 30, Ocorrencia = Ocorrencia.NaoObrigatoria)]
    public string CUF { get; set; }

    [DFeElement(TipoCampo.Str, "xServ", Ordem = 40, Ocorrencia = Ocorrencia.NaoObrigatoria)]
    public string XServ { get; set; } = "STATUS";
}
