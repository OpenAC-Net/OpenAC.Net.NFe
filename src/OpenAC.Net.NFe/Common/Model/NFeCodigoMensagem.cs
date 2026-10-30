using OpenAC.Net.DFe.Core.Attributes;
using OpenAC.Net.DFe.Core.Serializer;

namespace OpenAC.Net.NFe.Common.Model;

[DFeRoot("cMsg", Namespace = NFeConst.Namespace)]
public sealed partial class NFeCodigoMensagem : NFeMensagem
{
    [DFeItemValue(TipoCampo.Str)]
    public override string Valor { get; set; }
}
