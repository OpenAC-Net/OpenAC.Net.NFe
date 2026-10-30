using OpenAC.Net.DFe.Core.Attributes;
using OpenAC.Net.DFe.Core.Document;
using OpenAC.Net.DFe.Core.Serializer;

namespace OpenAC.Net.NFe.Common.Model;

[DFeRoot("infInut", Namespace = NFeConst.Namespace)]
public sealed partial class NFeRetInutilizacaoInfo : DFeDocument<NFeRetInutilizacaoInfo>
{
    [DFeAttribute(TipoCampo.Str, "Id", Ocorrencia = Ocorrencia.NaoObrigatoria)]
    public string Id { get; set; }

    [DFeElement(TipoCampo.Str, "tpAmb", Ordem = 20, Ocorrencia = Ocorrencia.NaoObrigatoria)]
    public string TpAmb { get; set; }

    [DFeElement(TipoCampo.Str, "verAplic", Ordem = 30, Ocorrencia = Ocorrencia.NaoObrigatoria)]
    public string VerAplic { get; set; }

    [DFeElement(TipoCampo.Int, "cStat", Ordem = 40, Ocorrencia = Ocorrencia.Obrigatoria)]
    public int CStat { get; set; }

    [DFeElement(TipoCampo.Str, "xMotivo", Ordem = 50, Ocorrencia = Ocorrencia.NaoObrigatoria)]
    public string XMotivo { get; set; }

    [DFeElement(TipoCampo.Str, "cUF", Ordem = 60, Ocorrencia = Ocorrencia.NaoObrigatoria)]
    public string CUF { get; set; }

    [DFeElement(TipoCampo.Str, "ano", Ordem = 70, Ocorrencia = Ocorrencia.NaoObrigatoria)]
    public string Ano { get; set; }

    [DFeElement(TipoCampo.Str, "CNPJ", Ordem = 80, Ocorrencia = Ocorrencia.NaoObrigatoria)]
    public string CNPJ { get; set; }

    [DFeElement(TipoCampo.Str, "mod", Ordem = 90, Ocorrencia = Ocorrencia.NaoObrigatoria)]
    public string Modelo { get; set; }

    [DFeElement(TipoCampo.Str, "serie", Ordem = 100, Ocorrencia = Ocorrencia.NaoObrigatoria)]
    public string Serie { get; set; }

    [DFeElement(TipoCampo.Str, "nNFIni", Ordem = 110, Ocorrencia = Ocorrencia.NaoObrigatoria)]
    public string NumeroInicial { get; set; }

    [DFeElement(TipoCampo.Str, "nNFFin", Ordem = 120, Ocorrencia = Ocorrencia.NaoObrigatoria)]
    public string NumeroFinal { get; set; }

    [DFeElement(TipoCampo.Str, "dhRecbto", Ordem = 130, Ocorrencia = Ocorrencia.NaoObrigatoria)]
    public string DataRecebimento { get; set; }

    [DFeElement(TipoCampo.Str, "nProt", Ordem = 140, Ocorrencia = Ocorrencia.NaoObrigatoria)]
    public string Protocolo { get; set; }
}
