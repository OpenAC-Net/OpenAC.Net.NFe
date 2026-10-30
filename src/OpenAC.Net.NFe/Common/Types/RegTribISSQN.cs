using OpenAC.Net.DFe.Core.Attributes;

namespace OpenAC.Net.NFe.Common.Types;

public enum RegTribISSQN
{
    [DFeEnum("1")]
    TISSMicroempresaMunicipal = 1,
    [DFeEnum("2")]
    RTISSEstimativa = 2,
    [DFeEnum("3")]
    RTISSSociedadeProfissionais = 3,
    [DFeEnum("4")]
    RTISSCooperativa = 4,
    [DFeEnum("5")]
    RTISSMEI = 5,
    [DFeEnum("6")]
    RTISSMEEPP = 6,
}