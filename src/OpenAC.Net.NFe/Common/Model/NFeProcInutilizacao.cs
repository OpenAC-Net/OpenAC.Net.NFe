using OpenAC.Net.DFe.Core.Attributes;
using OpenAC.Net.DFe.Core.Document;
using OpenAC.Net.DFe.Core.Serializer;

namespace OpenAC.Net.NFe.Common.Model;

[DFeRoot("ProcInutNFe", Namespace = NFeConst.Namespace)]
public sealed partial class NFeProcInutilizacao : DFeDocument<NFeProcInutilizacao>
{
    [DFeAttribute(TipoCampo.Str, "versao", Ocorrencia = Ocorrencia.Obrigatoria)]
    public string Versao { get; set; } = "4.00";

    [DFeElement("inutNFe", Ordem = 20, Ocorrencia = Ocorrencia.NaoObrigatoria)]
    public NFeInutilizacao Inutilizacao { get; set; }

    [DFeElement("retInutNFe", Ordem = 30, Ocorrencia = Ocorrencia.NaoObrigatoria)]
    public NFeInutilizacaoResult Retorno { get; set; }
}
