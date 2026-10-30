using OpenAC.Net.DFe.Core.Attributes;
using OpenAC.Net.DFe.Core.Document;
using OpenAC.Net.DFe.Core.Serializer;

namespace OpenAC.Net.NFe.Common.Model;

[DFeRoot("infEvento", Namespace = NFeConst.Namespace)]
public sealed partial class NFeRetEventoInfo : DFeDocument<NFeRetEventoInfo>
{
    [DFeAttribute(TipoCampo.Str, "Id", Ocorrencia = Ocorrencia.NaoObrigatoria)]
    public string Id { get; set; }

    [DFeElement(TipoCampo.Str, "tpAmb", Ordem = 20, Ocorrencia = Ocorrencia.NaoObrigatoria)]
    public string TpAmb { get; set; }

    [DFeElement(TipoCampo.Str, "verAplic", Ordem = 30, Ocorrencia = Ocorrencia.NaoObrigatoria)]
    public string VerAplic { get; set; }

    [DFeElement(TipoCampo.Str, "cOrgao", Ordem = 40, Ocorrencia = Ocorrencia.NaoObrigatoria)]
    public string Orgao { get; set; }

    [DFeElement(TipoCampo.Int, "cStat", Ordem = 50, Ocorrencia = Ocorrencia.Obrigatoria)]
    public int CStat { get; set; }

    [DFeElement(TipoCampo.Str, "xMotivo", Ordem = 60, Ocorrencia = Ocorrencia.NaoObrigatoria)]
    public string XMotivo { get; set; }

    [DFeElement(TipoCampo.Str, "chNFe", Ordem = 70, Ocorrencia = Ocorrencia.NaoObrigatoria)]
    public string Chave { get; set; }

    [DFeElement(TipoCampo.Str, "tpEvento", Ordem = 80, Ocorrencia = Ocorrencia.NaoObrigatoria)]
    public string Codigo { get; set; }

    [DFeElement(TipoCampo.Str, "xEvento", Ordem = 90, Ocorrencia = Ocorrencia.NaoObrigatoria)]
    public string Descricao { get; set; }

    [DFeElement(TipoCampo.Str, "nSeqEvento", Ordem = 100, Ocorrencia = Ocorrencia.NaoObrigatoria)]
    public string Sequencia { get; set; }

    [DFeElement(TipoCampo.Str, "cOrgaoAutor", Ordem = 115, Ocorrencia = Ocorrencia.NaoObrigatoria)]
    public string OrgaoAutor { get; set; }

    [DFeElement(TipoCampo.Str, "CNPJDest", Ordem = 110, Ocorrencia = Ocorrencia.NaoObrigatoria)]
    public string CNPJDest { get; set; }

    [DFeElement(TipoCampo.Str, "CPFDest", Ordem = 120, Ocorrencia = Ocorrencia.NaoObrigatoria)]
    public string CPFDest { get; set; }

    [DFeElement(TipoCampo.Str, "emailDest", Ordem = 130, Ocorrencia = Ocorrencia.NaoObrigatoria)]
    public string EmailDest { get; set; }

    [DFeElement(TipoCampo.Str, "dhRegEvento", Ordem = 140, Ocorrencia = Ocorrencia.NaoObrigatoria)]
    public string DataRegistro { get; set; }

    [DFeElement(TipoCampo.Str, "nProt", Ordem = 150, Ocorrencia = Ocorrencia.NaoObrigatoria)]
    public string Protocolo { get; set; }
}
