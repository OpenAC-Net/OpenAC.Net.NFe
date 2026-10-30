using OpenAC.Net.DFe.Core.Attributes;
using OpenAC.Net.DFe.Core.Document;
using OpenAC.Net.DFe.Core.Serializer;

namespace OpenAC.Net.NFe.Common.Model;

[DFeRoot("consChNFe", Namespace = NFeConst.Namespace)]
public sealed partial class NFeConsChNFe : DFeDocument<NFeConsChNFe>
{
    [DFeElement(TipoCampo.Str, "chNFe", Ordem = 10, Ocorrencia = Ocorrencia.NaoObrigatoria)]
    public string Chave { get; set; }
}
