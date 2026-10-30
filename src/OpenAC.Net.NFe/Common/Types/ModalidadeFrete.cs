using OpenAC.Net.DFe.Core.Attributes;

namespace OpenAC.Net.NFe.Common.Types;

public enum ModalidadeFrete
{
    [DFeEnum("0")]
    mfContaEmitente = 0,
    [DFeEnum("1")]
    mfContaDestinatario = 1,
    [DFeEnum("2")]
    mfContaTerceiros = 2,
    [DFeEnum("9")]
    mfSemFrete = 9,
    [DFeEnum("3")]
    Codigo3 = 3,
    [DFeEnum("4")]
    Codigo4 = 4,
}