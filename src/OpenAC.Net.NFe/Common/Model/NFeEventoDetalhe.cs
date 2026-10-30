using OpenAC.Net.DFe.Core.Attributes;
using OpenAC.Net.DFe.Core.Document;
using OpenAC.Net.DFe.Core.Serializer;

namespace OpenAC.Net.NFe.Common.Model;

[DFeRoot("detEvento", Namespace = NFeConst.Namespace)]
public partial class NFeEventoDetalhe : DFeDocument<NFeEventoDetalhe>
{
    [DFeAttribute(TipoCampo.Str, "versao", Ocorrencia = Ocorrencia.Obrigatoria)]
    public string Versao { get; set; } = "1.00";

    [DFeElement(TipoCampo.Str, "descEvento", Ordem = 20, Ocorrencia = Ocorrencia.NaoObrigatoria)]
    public string Descricao { get; set; }

    [DFeElement(TipoCampo.Str, "cOrgaoAutor", Ordem = 30, Ocorrencia = Ocorrencia.NaoObrigatoria)]
    public string OrgaoAutor { get; set; }

    [DFeElement(TipoCampo.Str, "tpAutor", Ordem = 40, Ocorrencia = Ocorrencia.NaoObrigatoria)]
    public string TipoAutor { get; set; }

    [DFeElement(TipoCampo.Str, "verAplic", Ordem = 50, Ocorrencia = Ocorrencia.NaoObrigatoria)]
    public string VersaoAplicativo { get; set; }

    [DFeElement(TipoCampo.Str, "nProt", Ordem = 60, Ocorrencia = Ocorrencia.NaoObrigatoria)]
    public string Protocolo { get; set; }

    [DFeElement(TipoCampo.Str, "xJust", Ordem = 70, Ocorrencia = Ocorrencia.NaoObrigatoria)]
    public string Justificativa { get; set; }

    [DFeElement(TipoCampo.Str, "xCorrecao", Ordem = 80, Ocorrencia = Ocorrencia.NaoObrigatoria)]
    public string Correcao { get; set; }

    [DFeElement(TipoCampo.Str, "xCondUso", Ordem = 90, Ocorrencia = Ocorrencia.NaoObrigatoria)]
    public string CondicaoUso { get; set; }

    [DFeElement(TipoCampo.Str, "chNFeRef", Ordem = 100, Ocorrencia = Ocorrencia.NaoObrigatoria)]
    public string ChaveReferenciada { get; set; }
}
