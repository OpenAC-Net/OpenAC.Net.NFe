using OpenAC.Net.DFe.Core.Attributes;
using OpenAC.Net.DFe.Core.Serializer;

namespace OpenAC.Net.NFe.Common.Model;

[DFeRoot("xMsg", Namespace = NFeConst.Namespace)]
public sealed partial class NFeTextoMensagem : NFeMensagem
{
    [DFeItemValue(TipoCampo.Str)]
    public override string Valor { get; set; }
}
