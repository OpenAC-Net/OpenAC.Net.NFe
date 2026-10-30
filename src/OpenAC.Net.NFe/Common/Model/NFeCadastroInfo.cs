using OpenAC.Net.DFe.Core.Attributes;
using OpenAC.Net.DFe.Core.Document;
using OpenAC.Net.DFe.Core.Serializer;

namespace OpenAC.Net.NFe.Common.Model;

[DFeRoot("infCad", Namespace = NFeConst.Namespace)]
public sealed partial class NFeCadastroInfo : DFeDocument<NFeCadastroInfo>
{
    [DFeElement(TipoCampo.Str, "IE", Ordem = 10, Ocorrencia = Ocorrencia.NaoObrigatoria)]
    public string IE { get; set; }

    [DFeElement(TipoCampo.Str, "CNPJ", Ordem = 20, Ocorrencia = Ocorrencia.NaoObrigatoria)]
    public string CNPJ { get; set; }

    [DFeElement(TipoCampo.Str, "CPF", Ordem = 30, Ocorrencia = Ocorrencia.NaoObrigatoria)]
    public string CPF { get; set; }

    [DFeElement(TipoCampo.Str, "UF", Ordem = 40, Ocorrencia = Ocorrencia.NaoObrigatoria)]
    public string UF { get; set; }

    [DFeElement(TipoCampo.Str, "cSit", Ordem = 50, Ocorrencia = Ocorrencia.NaoObrigatoria)]
    public string Situacao { get; set; }

    [DFeElement(TipoCampo.Str, "indCredNFe", Ordem = 60, Ocorrencia = Ocorrencia.NaoObrigatoria)]
    public string CredenciadoNFe { get; set; }

    [DFeElement(TipoCampo.Str, "indCredCTe", Ordem = 70, Ocorrencia = Ocorrencia.NaoObrigatoria)]
    public string CredenciadoCTe { get; set; }

    [DFeElement(TipoCampo.Str, "xNome", Ordem = 80, Ocorrencia = Ocorrencia.NaoObrigatoria)]
    public string Nome { get; set; }

    [DFeElement(TipoCampo.Str, "xFant", Ordem = 90, Ocorrencia = Ocorrencia.NaoObrigatoria)]
    public string Fantasia { get; set; }

    [DFeElement(TipoCampo.Str, "xRegApur", Ordem = 100, Ocorrencia = Ocorrencia.NaoObrigatoria)]
    public string RegimeApuracao { get; set; }

    [DFeElement(TipoCampo.Str, "CNAE", Ordem = 110, Ocorrencia = Ocorrencia.NaoObrigatoria)]
    public string CNAE { get; set; }

    [DFeElement(TipoCampo.Str, "dIniAtiv", Ordem = 120, Ocorrencia = Ocorrencia.NaoObrigatoria)]
    public string InicioAtividade { get; set; }

    [DFeElement(TipoCampo.Str, "dUltSit", Ordem = 130, Ocorrencia = Ocorrencia.NaoObrigatoria)]
    public string UltimaSituacao { get; set; }

    [DFeElement(TipoCampo.Str, "dBaixa", Ordem = 140, Ocorrencia = Ocorrencia.NaoObrigatoria)]
    public string Baixa { get; set; }

    [DFeElement(TipoCampo.Str, "IEUnica", Ordem = 150, Ocorrencia = Ocorrencia.NaoObrigatoria)]
    public string IEUnica { get; set; }

    [DFeElement(TipoCampo.Str, "IEAtual", Ordem = 160, Ocorrencia = Ocorrencia.NaoObrigatoria)]
    public string IEAtual { get; set; }

    [DFeElement("ender", Ordem = 170, Ocorrencia = Ocorrencia.NaoObrigatoria)]
    public NFeCadastroEndereco Endereco { get; set; }
}
