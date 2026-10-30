using OpenAC.Net.DFe.Core.Attributes;
using OpenAC.Net.DFe.Core.Collection;
using OpenAC.Net.DFe.Core.Document;
using OpenAC.Net.DFe.Core.Serializer;

namespace OpenAC.Net.NFe.Common.Model;

[DFeRoot("infProt", Namespace = NFeConst.Namespace)]
public sealed partial class NFeInfProt : DFeDocument<NFeInfProt>
{
    [DFeAttribute(TipoCampo.Str, "Id", Ocorrencia = Ocorrencia.NaoObrigatoria)]
    public string Id { get; set; }

    [DFeElement(TipoCampo.Str, "tpAmb", Ordem = 20, Ocorrencia = Ocorrencia.NaoObrigatoria)]
    public string TpAmb { get; set; }

    [DFeElement(TipoCampo.Str, "verAplic", Ordem = 30, Ocorrencia = Ocorrencia.NaoObrigatoria)]
    public string VerAplic { get; set; }

    [DFeElement(TipoCampo.Str, "chNFe", Ordem = 40, Ocorrencia = Ocorrencia.NaoObrigatoria)]
    public string ChNFe { get; set; }

    [DFeElement(TipoCampo.Str, "dhRecbto", Ordem = 50, Ocorrencia = Ocorrencia.NaoObrigatoria)]
    public string DhRecbto { get; set; }

    [DFeElement(TipoCampo.Str, "nProt", Ordem = 60, Ocorrencia = Ocorrencia.NaoObrigatoria)]
    public string NProt { get; set; }

    [DFeElement(TipoCampo.Str, "digVal", Ordem = 70, Ocorrencia = Ocorrencia.NaoObrigatoria)]
    public string DigVal { get; set; }

    [DFeElement(TipoCampo.Int, "cStat", Ordem = 80, Ocorrencia = Ocorrencia.Obrigatoria)]
    public int CStat { get; set; }

    [DFeElement(TipoCampo.Str, "xMotivo", Ordem = 90, Ocorrencia = Ocorrencia.NaoObrigatoria)]
    public string XMotivo { get; set; }

    [DFeCollection("", Ordem = 120, Ocorrencia = Ocorrencia.NaoObrigatoria)]
    [DFeItem(typeof(NFeCodigoMensagem), "cMsg")]
    [DFeItem(typeof(NFeTextoMensagem), "xMsg")]
    public DFeCollection<NFeMensagem> Mensagens { get; set; } = new();
}
