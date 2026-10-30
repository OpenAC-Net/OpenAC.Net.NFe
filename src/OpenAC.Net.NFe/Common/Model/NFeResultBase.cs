using OpenAC.Net.DFe.Core.Attributes;
using OpenAC.Net.DFe.Core.Document;
using OpenAC.Net.DFe.Core.Serializer;

namespace OpenAC.Net.NFe.Common.Model;

public abstract class NFeResultBase<T> : DFeDocument<T> where T : class
{
    [DFeAttribute(TipoCampo.Str, "versao", Ocorrencia = Ocorrencia.NaoObrigatoria)]
    public string Versao { get; set; }

    [DFeElement(TipoCampo.Str, "tpAmb", Ordem = 10, Ocorrencia = Ocorrencia.NaoObrigatoria)]
    public string TpAmb { get; set; }

    [DFeElement(TipoCampo.Str, "verAplic", Ordem = 20, Ocorrencia = Ocorrencia.NaoObrigatoria)]
    public string VerAplic { get; set; }

    [DFeElement(TipoCampo.Int, "cStat", Ordem = 30, Ocorrencia = Ocorrencia.Obrigatoria)]
    public int CStat { get; set; }

    [DFeElement(TipoCampo.Str, "xMotivo", Ordem = 40, Ocorrencia = Ocorrencia.NaoObrigatoria)]
    public string XMotivo { get; set; }

    [DFeElement(TipoCampo.Str, "cUF", Ordem = 50, Ocorrencia = Ocorrencia.NaoObrigatoria)]
    public string CUF { get; set; }

    [DFeElement(TipoCampo.Str, "dhRecbto", Ordem = 60, Ocorrencia = Ocorrencia.NaoObrigatoria)]
    public string DhRecbto { get; set; }

    [DFeIgnore]
    public virtual int Codigo => CStat;

    [DFeIgnore]
    public virtual string Motivo => XMotivo ?? "";

    protected virtual bool ShouldSerializeCStat() => true;
}
