using OpenAC.Net.DFe.Core.Attributes;
using OpenAC.Net.DFe.Core.Document;
using OpenAC.Net.DFe.Core.Serializer;

namespace OpenAC.Net.NFe.Common.Model;

[DFeRoot("infInut", Namespace = NFeConst.Namespace)]
public sealed partial class NFeInutilizacaoInfo : DFeDocument<NFeInutilizacaoInfo>
{
    [DFeAttribute(TipoCampo.Str, "Id", Ocorrencia = Ocorrencia.Obrigatoria)]
    public string Id { get; set; }

    [DFeElement(TipoCampo.Str, "tpAmb", Ordem = 20, Ocorrencia = Ocorrencia.NaoObrigatoria)]
    public string TpAmb { get; set; }

    [DFeElement(TipoCampo.Str, "xServ", Ordem = 30, Ocorrencia = Ocorrencia.NaoObrigatoria)]
    public string XServ { get; set; } = "INUTILIZAR";

    [DFeElement(TipoCampo.Str, "cUF", Ordem = 40, Ocorrencia = Ocorrencia.NaoObrigatoria)]
    public string CUF { get; set; }

    [DFeElement(TipoCampo.Str, "ano", Ordem = 50, Ocorrencia = Ocorrencia.NaoObrigatoria)]
    public string Ano { get; set; }

    [DFeElement(TipoCampo.Str, "CNPJ", Ordem = 60, Ocorrencia = Ocorrencia.NaoObrigatoria)]
    public string CNPJ { get; set; }

    [DFeElement(TipoCampo.Str, "mod", Ordem = 70, Ocorrencia = Ocorrencia.NaoObrigatoria)]
    public string Modelo { get; set; }

    [DFeElement(TipoCampo.Str, "serie", Ordem = 80, Ocorrencia = Ocorrencia.NaoObrigatoria)]
    public string Serie { get; set; }

    [DFeElement(TipoCampo.Str, "nNFIni", Ordem = 90, Ocorrencia = Ocorrencia.NaoObrigatoria)]
    public string NumeroInicial { get; set; }

    [DFeElement(TipoCampo.Str, "nNFFin", Ordem = 100, Ocorrencia = Ocorrencia.NaoObrigatoria)]
    public string NumeroFinal { get; set; }

    [DFeElement(TipoCampo.Str, "xJust", Ordem = 110, Ocorrencia = Ocorrencia.NaoObrigatoria)]
    public string Justificativa { get; set; }
}
