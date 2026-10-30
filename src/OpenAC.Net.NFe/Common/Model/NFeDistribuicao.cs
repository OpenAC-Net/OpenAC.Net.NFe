using OpenAC.Net.DFe.Core.Attributes;
using OpenAC.Net.DFe.Core.Document;
using OpenAC.Net.DFe.Core.Serializer;

namespace OpenAC.Net.NFe.Common.Model;

[DFeRoot("distDFeInt", Namespace = NFeConst.Namespace)]
public sealed partial class NFeDistribuicao : DFeDocument<NFeDistribuicao>
{
    [DFeAttribute(TipoCampo.Str, "versao", Ocorrencia = Ocorrencia.Obrigatoria)]
    public string Versao { get; set; } = "1.01";

    [DFeElement(TipoCampo.Str, "tpAmb", Ordem = 20, Ocorrencia = Ocorrencia.NaoObrigatoria)]
    public string TpAmb { get; set; }

    [DFeElement(TipoCampo.Str, "cUFAutor", Ordem = 30, Ocorrencia = Ocorrencia.NaoObrigatoria)]
    public string CUFAutor { get; set; }

    [DFeElement(TipoCampo.Str, "CNPJ", Ordem = 40, Ocorrencia = Ocorrencia.NaoObrigatoria)]
    public string CNPJ { get; set; }

    [DFeElement(TipoCampo.Str, "CPF", Ordem = 50, Ocorrencia = Ocorrencia.NaoObrigatoria)]
    public string CPF { get; set; }

    [DFeElement("distNSU", Ordem = 60, Ocorrencia = Ocorrencia.NaoObrigatoria)]
    public NFeDistNSU DistNSU { get; set; }

    [DFeElement("consNSU", Ordem = 70, Ocorrencia = Ocorrencia.NaoObrigatoria)]
    public NFeConsNSU ConsNSU { get; set; }

    [DFeElement("consChNFe", Ordem = 80, Ocorrencia = Ocorrencia.NaoObrigatoria)]
    public NFeConsChNFe ConsChNFe { get; set; }
}
