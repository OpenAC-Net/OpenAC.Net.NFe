using OpenAC.Net.DFe.Core.Attributes;
using OpenAC.Net.DFe.Core.Document;
using OpenAC.Net.DFe.Core.Serializer;

namespace OpenAC.Net.NFe.Common.Model;

[DFeRoot("infEvento", Namespace = NFeConst.Namespace)]
public sealed partial class NFeEventoInfo : DFeDocument<NFeEventoInfo>
{
    [DFeAttribute(TipoCampo.Str, "Id", Ocorrencia = Ocorrencia.Obrigatoria)]
    public string Id { get; set; }

    [DFeElement(TipoCampo.Str, "cOrgao", Ordem = 20, Ocorrencia = Ocorrencia.NaoObrigatoria)]
    public string Orgao { get; set; }

    [DFeElement(TipoCampo.Str, "tpAmb", Ordem = 30, Ocorrencia = Ocorrencia.NaoObrigatoria)]
    public string TpAmb { get; set; }

    [DFeElement(TipoCampo.Str, "CNPJ", Ordem = 40, Ocorrencia = Ocorrencia.NaoObrigatoria)]
    public string CNPJ { get; set; }

    [DFeElement(TipoCampo.Str, "CPF", Ordem = 50, Ocorrencia = Ocorrencia.NaoObrigatoria)]
    public string CPF { get; set; }

    [DFeElement(TipoCampo.Str, "chNFe", Ordem = 60, Ocorrencia = Ocorrencia.NaoObrigatoria)]
    public string Chave { get; set; }

    [DFeElement(TipoCampo.Str, "dhEvento", Ordem = 70, Ocorrencia = Ocorrencia.NaoObrigatoria)]
    public string Data { get; set; }

    [DFeElement(TipoCampo.Str, "tpEvento", Ordem = 80, Ocorrencia = Ocorrencia.NaoObrigatoria)]
    public string Codigo { get; set; }

    [DFeElement(TipoCampo.Str, "nSeqEvento", Ordem = 90, Ocorrencia = Ocorrencia.NaoObrigatoria)]
    public string Sequencia { get; set; }

    [DFeElement(TipoCampo.Str, "verEvento", Ordem = 100, Ocorrencia = Ocorrencia.NaoObrigatoria)]
    public string Versao { get; set; }

    [DFeElement("detEvento", Ordem = 110, Ocorrencia = Ocorrencia.NaoObrigatoria)]
    public NFeEventoDetalhe Detalhe { get; set; }
}
