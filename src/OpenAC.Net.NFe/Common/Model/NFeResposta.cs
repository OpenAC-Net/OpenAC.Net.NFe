using OpenAC.Net.DFe.Core.Service;

namespace OpenAC.Net.NFe.Common.Model;

public abstract class NFeResposta<T>(string xmlEnvio, string xmlRetorno, string envelopeEnvio, string envelopeRetorno)
    : DFeResposta<T>(xmlEnvio, xmlRetorno, envelopeEnvio, envelopeRetorno) where T : NFeResultBase<T>
{
    public int CStat => Resultado.Codigo;
    public string XMotivo => Resultado.Motivo;
}
