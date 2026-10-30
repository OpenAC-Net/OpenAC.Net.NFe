using OpenAC.Net.DFe.Core.Attributes;
using OpenAC.Net.DFe.Core.Document;
using OpenAC.Net.DFe.Core.Serializer;

namespace OpenAC.Net.NFe.Common.Model;

[DFeRoot("ConsCad", Namespace = NFeConst.Namespace)]
public sealed partial class NFeConsultaCadastro : DFeDocument<NFeConsultaCadastro>
{
    [DFeAttribute(TipoCampo.Str, "versao", Ocorrencia = Ocorrencia.Obrigatoria)]
    public string Versao { get; set; } = "2.00";

    [DFeElement("infCons", Ordem = 20, Ocorrencia = Ocorrencia.NaoObrigatoria)]
    public NFeConsultaCadastroInfo InfCons { get; set; }
}
