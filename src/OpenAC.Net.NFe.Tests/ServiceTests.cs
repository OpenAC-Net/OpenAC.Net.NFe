using OpenAC.Net.NFe.Common;
using OpenAC.Net.NFe.Common.Model;
using OpenAC.Net.NFe.Common.Types;
using System.IO.Compression;
using System.Text;
using System.Xml.Linq;
using OpenAC.Net.NFe.DANFe.PDFSharp;
using OpenAC.Net.NFe.Services;
using Xunit;

namespace OpenAC.Net.NFe.Tests;

public sealed class ServiceTests
{
    [Fact]public async Task LeiauteAntigoNuncaAcessaRede()
    {
        var t=new FakeTransport();var config=new NFeConfig();config.Geral.VersaoDFe=NFeVersao.v310;var client=Fixtures.Client(config,t);
        await Assert.ThrowsAsync<NotSupportedException>(()=>client.ConsultarSituacaoServicoAsync());
        await Assert.ThrowsAsync<NotSupportedException>(()=>client.EnviarAsync(new NFeEnvioSolicitacao()));Assert.Equal(0,t.Calls);
    }
    [Fact]
    public async Task StatusUsaSoapEspecificoDeSP()
    {
        var transporte = new FakeTransport();
        var cliente = Fixtures.Client(new NFeConfig(), transporte);
        var resposta = await cliente.ConsultarSituacaoServicoAsync();
        Assert.Equal(107, resposta.CStat);
        const string contrato = "http://www.portalfiscal.inf.br/nfe/wsdl/NfeStatusServico4";
        Assert.Equal(contrato + "/nfeStatusServicoNF", transporte.Request.Action);
        Assert.Contains("homologacao", transporte.Request.Endereco.AbsoluteUri);
        var envelope = XElement.Parse(transporte.Request.Envelope);
        XNamespace soap = "http://www.w3.org/2003/05/soap-envelope";
        var mensagem = Assert.Single(envelope.Element(soap + "Body")!.Elements());
        Assert.Equal(XName.Get("nfeDadosMsg", contrato), mensagem.Name);
        Assert.Equal(XmlFixture.Ns + "consStatServ", Assert.Single(mensagem.Elements()).Name);
        Assert.Contains("<cUF>35</cUF>", resposta.XmlEnvio);
        Assert.DoesNotContain("<?xml", resposta.XmlEnvio);
        Assert.Contains(resposta.XmlEnvio, transporte.Request.Envelope, StringComparison.Ordinal);
    }
    [Fact]public async Task AutorizacaoSincronaProtocolaSomenteNotaCorrespondente()
    {
        using var cert=Fixtures.Certificate();var config=Fixtures.Config(cert,65);var nota=Fixtures.Nota(65);nota.Assinar(cert);var t=new FakeTransport();
        t.Response=_=>new XElement(XmlFixture.Ns+"retEnviNFe",new XElement(XmlFixture.Ns+"cStat","104"),Fixtures.Protocolo(nota).WriteToXml()).ToString(SaveOptions.DisableFormatting);
        var client=Fixtures.Client(config,t);
        var result=await client.EnviarAsync(new NFeEnvioSolicitacao { Lote = "123", Documentos = [nota] });Assert.Single(result.Documentos);Assert.True(result.Documentos[0].Autorizado);Assert.True(result.Documentos[0].ValidarAssinatura());
        Assert.Contains("<indSinc>1</indSinc>",t.Request.Envelope);Assert.Equal(1,t.Calls);
    }
    [Fact]public async Task RejeicaoFiscalNaoViraNotaAutorizada()
    {
        using var cert=Fixtures.Certificate();var nota=Fixtures.Nota();nota.Assinar(cert);var t=new FakeTransport{Response=_=>new XElement(XmlFixture.Ns+"retEnviNFe",new XElement(XmlFixture.Ns+"cStat","104"),Fixtures.Protocolo(nota,"204").WriteToXml()).ToString()};
        var client=Fixtures.Client(Fixtures.Config(cert),t);Assert.Empty((await client.EnviarAsync(new NFeEnvioSolicitacao { Documentos =
            [nota]
        })).Documentos);
    }
    [Fact]public async Task NfceNaoAceitaLoteAssincrono()
    {
        var t=new FakeTransport();var client=Fixtures.Client(Fixtures.Config(null,65),t);await Assert.ThrowsAsync<ArgumentException>(()=>client.EnviarAsync(new NFeEnvioSolicitacao { Sincrono = false, Documentos =
            [Fixtures.Nota(65)]
        }));Assert.Equal(0,t.Calls);
    }
    [Fact]public async Task CancelamentoValidaEventoEImprime()
    {
        using var cert=Fixtures.Certificate();var nota=Fixtures.Nota();var config=Fixtures.Config(cert);var evento=NFeEvento.Cancelamento(nota.Chave,"12345678000195","135260000000001","Cancelamento de teste solicitado pelo cliente");
        var t=new FakeTransport{Response=_=>"<retEnvEvento xmlns='http://www.portalfiscal.inf.br/nfe'><cStat>128</cStat><xMotivo>Lote de Evento Processado</xMotivo></retEnvEvento>"};
        var client=Fixtures.Client(config,t);Assert.Equal(128,(await client.EnviarEventoAsync(new NFeEventoSolicitacao { Eventos =
            [evento]
        })).CStat);
        var signed=evento.Assinar(config);var pdf=new OpenDANFe().GerarEventoPDF(signed);Assert.True(pdf.Length>1000);
        var dir=Path.Combine(AppContext.BaseDirectory,"Artifacts");Directory.CreateDirectory(dir);File.WriteAllBytes(Path.Combine(dir,"evento-cancelamento.pdf"),pdf);
    }
    [Fact]public void CartaCorrecaoNaoPermiteNfce()
    {
        using var cert=Fixtures.Certificate();var evt=NFeEvento.CartaCorrecao(Fixtures.Nota(65).Chave,"12345678000195",1,"Correcao de informacao complementar");Assert.Throws<NotSupportedException>(()=>evt.Assinar(Fixtures.Config(cert,65)));
    }
    [Fact]public async Task CancelamentoAntesDaChamadaNaoAcessaRede()
    {
        var t=new FakeTransport();var client=Fixtures.Client(new NFeConfig(),t);using var cts=new CancellationTokenSource();cts.Cancel();await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>client.ConsultarSituacaoServicoAsync(cts.Token));Assert.Equal(0,t.Calls);
    }
    [Fact]public void DistribuicaoDescompactaDocZip()
    {
        using var output=new MemoryStream();using(var zip=new GZipStream(output,CompressionLevel.Optimal,true)){var b=Encoding.UTF8.GetBytes("<resNFe>teste</resNFe>");zip.Write(b,0,b.Length);}
        var r=new NFeDistribuicaoResposta("", new XElement(XmlFixture.Ns+"retDistDFeInt",new XElement(XmlFixture.Ns+"loteDistDFeInt",new XElement(XmlFixture.Ns+"docZip",new XAttribute("NSU","000000000000001"),new XAttribute("schema","resNFe_v1.01.xsd"),Convert.ToBase64String(output.ToArray())))).ToString(SaveOptions.DisableFormatting));
        Assert.Equal("<resNFe>teste</resNFe>",Assert.Single(r.DocumentosDistribuidos()).Xml);
    }
    [Fact]public void CatalogoResolveTodasAsUFsSeparadamente()
    {
        foreach(var model in new[]{ModeloNFe.NFe,ModeloNFe.NFCe})foreach(var uf in Enum.GetValues<OpenAC.Net.DFe.Core.Common.DFeSiglaUF>())
        {
            if(!Enum.IsDefined(typeof(OpenAC.Net.DFe.Core.Common.DFeCodUF),uf.ToString()) || uf.ToString() is "AN" or "EX" or "SU")continue;
            var c=new NFeConfig();c.Geral.Modelo=model;c.WebServices.UF=uf;
            Assert.Equal("https",new Uri(NFeServiceManager.ObterEndereco(c,TipoServicoNFe.NFeAutorizacao)).Scheme);
        }
    }
}
