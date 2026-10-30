using OpenAC.Net.DFe.Core.Attributes;

namespace OpenAC.Net.NFe.Common.Types;

public enum PresencaComprador
{
    [DFeEnum("0")]
    Nao = 0,
    [DFeEnum("1")]
    Presencial = 1,
    [DFeEnum("2")]
    Internet = 2,
    [DFeEnum("3")]
    Teleatendimento = 3,
    [DFeEnum("4")]
    EntregaDomicilio = 4,
    [DFeEnum("9")]
    Outros = 9,
    [DFeEnum("5")]
    Codigo5 = 5,
}