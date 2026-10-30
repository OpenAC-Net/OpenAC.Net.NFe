using OpenAC.Net.DFe.Core.Attributes;

namespace OpenAC.Net.NFe.Common.Types;

public enum TipoArma
{
    [DFeEnum("0")]
    Permitido = 0,
    [DFeEnum("1")]
    Restrito = 1,
}