using OpenAC.Net.DFe.Core.Attributes;
using OpenAC.Net.DFe.Core.Collection;
using OpenAC.Net.DFe.Core.Document;

namespace OpenAC.Net.NFe.Common.Model;

[DFeRoot("loteDistDFeInt", Namespace = NFeConst.Namespace)]
public sealed partial class NFeDistribuicaoLote : DFeDocument<NFeDistribuicaoLote>
{
    [DFeCollection("docZip", Ordem = 10, Ocorrencia = Ocorrencia.NaoObrigatoria)]
    public DFeCollection<NFeDocZip> Documentos { get; set; } = new();
}
