using OpenAC.Net.DFe.Core.Attributes;
using OpenAC.Net.DFe.Core.Collection;
using OpenAC.Net.DFe.Core.Document;
using OpenAC.Net.DFe.Core.Serializer;

namespace OpenAC.Net.NFe.Common.Model;

[DFeRoot("infCons", Namespace = NFeConst.Namespace)]
public sealed partial class NFeRetCadastroInfo : DFeDocument<NFeRetCadastroInfo>
{
    [DFeElement(TipoCampo.Str, "verAplic", Ordem = 10, Ocorrencia = Ocorrencia.NaoObrigatoria)]
    public string VerAplic { get; set; }

    [DFeElement(TipoCampo.Int, "cStat", Ordem = 20, Ocorrencia = Ocorrencia.Obrigatoria)]
    public int CStat { get; set; }

    [DFeElement(TipoCampo.Str, "xMotivo", Ordem = 30, Ocorrencia = Ocorrencia.NaoObrigatoria)]
    public string XMotivo { get; set; }

    [DFeElement(TipoCampo.Str, "UF", Ordem = 40, Ocorrencia = Ocorrencia.NaoObrigatoria)]
    public string UF { get; set; }

    [DFeElement(TipoCampo.Str, "IE", Ordem = 50, Ocorrencia = Ocorrencia.NaoObrigatoria)]
    public string IE { get; set; }

    [DFeElement(TipoCampo.Str, "CNPJ", Ordem = 60, Ocorrencia = Ocorrencia.NaoObrigatoria)]
    public string CNPJ { get; set; }

    [DFeElement(TipoCampo.Str, "CPF", Ordem = 70, Ocorrencia = Ocorrencia.NaoObrigatoria)]
    public string CPF { get; set; }

    [DFeElement(TipoCampo.Str, "dhCons", Ordem = 80, Ocorrencia = Ocorrencia.NaoObrigatoria)]
    public string DataConsulta { get; set; }

    [DFeElement(TipoCampo.Str, "cUF", Ordem = 90, Ocorrencia = Ocorrencia.NaoObrigatoria)]
    public string CUF { get; set; }

    [DFeCollection("infCad", Ordem = 100, Ocorrencia = Ocorrencia.NaoObrigatoria)]
    public DFeCollection<NFeCadastroInfo> Cadastros { get; set; } = new();
}
