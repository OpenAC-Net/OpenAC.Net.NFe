using OpenAC.Net.DFe.Core.Attributes;

namespace OpenAC.Net.NFe.Common.Types;

public enum BandeiraCartao
{
    [DFeEnum("01")]
    bcVisa = 1,
    [DFeEnum("02")]
    bcMasterCard = 2,
    [DFeEnum("03")]
    bcAmericanExpress = 3,
    [DFeEnum("04")]
    bcSorocred = 4,
    [DFeEnum("99")]
    bcOutros = 99,
}