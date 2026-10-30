using OpenAC.Net.DFe.Core.Attributes;
using OpenAC.Net.DFe.Core.Document;
using OpenAC.Net.DFe.Core.Serializer;

namespace OpenAC.Net.NFe.Common.Model;

[DFeRoot("infCons", Namespace = NFeConst.Namespace)]
public sealed partial class NFeConsultaCadastroInfo : DFeDocument<NFeConsultaCadastroInfo>
{
    [DFeElement(TipoCampo.Str, "xServ", Ordem = 10, Ocorrencia = Ocorrencia.NaoObrigatoria)]
    public string XServ { get; set; } = "CONS-CAD";

    [DFeElement(TipoCampo.Str, "UF", Ordem = 20, Ocorrencia = Ocorrencia.NaoObrigatoria)]
    public string UF { get; set; }

    [DFeElement(TipoCampo.Str, "IE", Ordem = 30, Ocorrencia = Ocorrencia.NaoObrigatoria)]
    public string IE { get; set; }

    [DFeElement(TipoCampo.Str, "CNPJ", Ordem = 40, Ocorrencia = Ocorrencia.NaoObrigatoria)]
    public string CNPJ { get; set; }

    [DFeElement(TipoCampo.Str, "CPF", Ordem = 50, Ocorrencia = Ocorrencia.NaoObrigatoria)]
    public string CPF { get; set; }
}
