using OpenAC.Net.DFe.Core.Attributes;
using OpenAC.Net.DFe.Core.Document;
using OpenAC.Net.DFe.Core.Serializer;

namespace OpenAC.Net.NFe.Common.Model;

[DFeRoot("ender", Namespace = NFeConst.Namespace)]
public sealed partial class NFeCadastroEndereco : DFeDocument<NFeCadastroEndereco>
{
    [DFeElement(TipoCampo.Str, "xLgr", Ordem = 10, Ocorrencia = Ocorrencia.NaoObrigatoria)]
    public string Logradouro { get; set; }

    [DFeElement(TipoCampo.Str, "nro", Ordem = 20, Ocorrencia = Ocorrencia.NaoObrigatoria)]
    public string Numero { get; set; }

    [DFeElement(TipoCampo.Str, "xCpl", Ordem = 30, Ocorrencia = Ocorrencia.NaoObrigatoria)]
    public string Complemento { get; set; }

    [DFeElement(TipoCampo.Str, "xBairro", Ordem = 40, Ocorrencia = Ocorrencia.NaoObrigatoria)]
    public string Bairro { get; set; }

    [DFeElement(TipoCampo.Str, "cMun", Ordem = 50, Ocorrencia = Ocorrencia.NaoObrigatoria)]
    public string CodigoMunicipio { get; set; }

    [DFeElement(TipoCampo.Str, "xMun", Ordem = 60, Ocorrencia = Ocorrencia.NaoObrigatoria)]
    public string Municipio { get; set; }

    [DFeElement(TipoCampo.Str, "CEP", Ordem = 70, Ocorrencia = Ocorrencia.NaoObrigatoria)]
    public string CEP { get; set; }
}
