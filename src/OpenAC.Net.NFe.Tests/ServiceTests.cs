using System.IO;
using OpenAC.Net.NFe.Common;
using OpenAC.Net.NFe.Common.Model;
using OpenAC.Net.NFe.Common.Types;
using System.IO.Compression;
using System.Text;
using System.Xml.Linq;
using OpenAC.Net.NFe.DANFe.PDFSharp;
using OpenAC.Net.NFe.Services;
using System.Threading.Tasks;

namespace OpenAC.Net.NFe.Tests;

public sealed class ServiceTests
{
    [Test]public async Task LeiauteAntigoNuncaAcessaRede()
    {
        var t=new FakeTransport();var config=new NFeConfig();config.Geral.VersaoDFe=NFeVersao.v310;var client=Fixtures.Client(config,t);
        await Assert.That(()=>client.ConsultarSituacaoServicoAsync()).ThrowsExactly<NotSupportedException>();
        await Assert.That(()=>client.EnviarAsync(new NFeEnvioSolicitacao())).ThrowsExactly<NotSupportedException>(); await Assert.That(t.Calls).IsEqualTo(0);
    }
    [Test]
    public async Task StatusUsaSoapEspecificoDeSP()
    {
        var transporte = new FakeTransport();
        var cliente = Fixtures.Client(new NFeConfig(), transporte);
        var resposta = await cliente.ConsultarSituacaoServicoAsync();
        await Assert.That(resposta.CStat).IsEqualTo(107);
        const string contrato = "http://www.portalfiscal.inf.br/nfe/wsdl/NfeStatusServico4";
        await Assert.That(transporte.Request.Action).IsEqualTo(contrato + "/nfeStatusServicoNF");
        await Assert.That(transporte.Request.Endereco.AbsoluteUri).Contains("homologacao");
        var envelope = XElement.Parse(transporte.Request.Envelope);
        XNamespace soap = "http://www.w3.org/2003/05/soap-envelope";
        var mensagem = await Assert.That(envelope.Element(soap + "Body")!.Elements()).HasSingleItem();
        await Assert.That(mensagem.Name).IsEqualTo(XName.Get("nfeDadosMsg", contrato));
        await Assert.That((await Assert.That(mensagem.Elements()).HasSingleItem()).Name).IsEqualTo(XmlFixture.Ns + "consStatServ");
        await Assert.That(resposta.XmlEnvio).Contains("<cUF>35</cUF>");
        await Assert.That(resposta.XmlEnvio).DoesNotContain("<?xml");
        await Assert.That(transporte.Request.Envelope).Contains(resposta.XmlEnvio);
    }
    [Test]public async Task AutorizacaoSincronaProtocolaSomenteNotaCorrespondente()
    {
        using var cert=Fixtures.Certificate();var config=Fixtures.Config(cert,65);var nota=Fixtures.Nota(65);nota.Assinar(cert);var t=new FakeTransport();
        t.Response=_=>new XElement(XmlFixture.Ns+"retEnviNFe",new XElement(XmlFixture.Ns+"cStat","104"),Fixtures.Protocolo(nota).WriteToXml()).ToString(SaveOptions.DisableFormatting);
        var client=Fixtures.Client(config,t);
        var result=await client.EnviarAsync(new NFeEnvioSolicitacao { Lote = "123", Documentos = [nota] });await Assert.That(result.Documentos).HasSingleItem(); await Assert.That(result.Documentos[0].Autorizado).IsTrue(); await Assert.That(result.Documentos[0].ValidarAssinatura()).IsTrue();
        await Assert.That(t.Request.Envelope).Contains("<indSinc>1</indSinc>"); await Assert.That(t.Calls).IsEqualTo(1);
    }
    [Test]public async Task RejeicaoFiscalNaoViraNotaAutorizada()
    {
        using var cert=Fixtures.Certificate();var nota=Fixtures.Nota();nota.Assinar(cert);var t=new FakeTransport{Response=_=>new XElement(XmlFixture.Ns+"retEnviNFe",new XElement(XmlFixture.Ns+"cStat","104"),Fixtures.Protocolo(nota,"204").WriteToXml()).ToString()};
        var client=Fixtures.Client(Fixtures.Config(cert),t);await Assert.That((await client.EnviarAsync(new NFeEnvioSolicitacao { Documentos =
            [nota]
        })).Documentos).IsEmpty();
    }
    [Test]public async Task NfceNaoAceitaLoteAssincrono()
    {
        var t=new FakeTransport();var client=Fixtures.Client(Fixtures.Config(null,65),t);await Assert.That(()=>client.EnviarAsync(new NFeEnvioSolicitacao { Sincrono = false, Documentos =
            [Fixtures.Nota(65)]
        })).ThrowsExactly<ArgumentException>(); await Assert.That(t.Calls).IsEqualTo(0);
    }
    [Test]public async Task CancelamentoValidaEventoEImprime()
    {
        using var cert=Fixtures.Certificate();var nota=Fixtures.Nota();var config=Fixtures.Config(cert);var evento=NFeEvento.Cancelamento(nota.Chave,"12345678000195","135260000000001","Cancelamento de teste solicitado pelo cliente");
        var t=new FakeTransport{Response=_=>"<retEnvEvento xmlns='http://www.portalfiscal.inf.br/nfe'><cStat>128</cStat><xMotivo>Lote de Evento Processado</xMotivo></retEnvEvento>"};
        var client=Fixtures.Client(config,t);await Assert.That((await client.EnviarEventoAsync(new NFeEventoSolicitacao { Eventos =
            [evento]
        })).CStat).IsEqualTo(128);
        var signed=evento.Assinar(config);var pdf=new OpenDANFe().GerarEventoPDF(signed);await Assert.That(pdf.Length>1000).IsTrue();
        var dir=Path.Combine(AppContext.BaseDirectory,"Artifacts");Directory.CreateDirectory(dir);File.WriteAllBytes(Path.Combine(dir,"evento-cancelamento.pdf"),pdf);
    }
    [Test]public async Task CartaCorrecaoNaoPermiteNfce()
    {
        using var cert=Fixtures.Certificate();var evt=NFeEvento.CartaCorrecao(Fixtures.Nota(65).Chave,"12345678000195",1,"Correcao de informacao complementar");await Assert.That(()=>evt.Assinar(Fixtures.Config(cert,65))).ThrowsExactly<NotSupportedException>();
    }
    [Test]public async Task CancelamentoAntesDaChamadaNaoAcessaRede()
    {
        var t=new FakeTransport();var client=Fixtures.Client(new NFeConfig(),t);using var cts=new CancellationTokenSource();cts.Cancel();await Assert.That(()=>client.ConsultarSituacaoServicoAsync(cts.Token)).Throws<OperationCanceledException>(); await Assert.That(t.Calls).IsEqualTo(0);
    }
    [Test]public async Task DistribuicaoDescompactaDocZip()
    {
        using var output=new MemoryStream();using(var zip=new GZipStream(output,CompressionLevel.Optimal,true)){var b=Encoding.UTF8.GetBytes("<resNFe>teste</resNFe>");zip.Write(b,0,b.Length);}
        var r=new NFeDistribuicaoResposta("", new XElement(XmlFixture.Ns+"retDistDFeInt",new XElement(XmlFixture.Ns+"loteDistDFeInt",new XElement(XmlFixture.Ns+"docZip",new XAttribute("NSU","000000000000001"),new XAttribute("schema","resNFe_v1.01.xsd"),Convert.ToBase64String(output.ToArray())))).ToString(SaveOptions.DisableFormatting));
        await Assert.That((await Assert.That(r.DocumentosDistribuidos()).HasSingleItem()).Xml).IsEqualTo("<resNFe>teste</resNFe>");
    }
    [Test]public async Task CatalogoResolveTodasAsUFsSeparadamente()
    {
        foreach(var model in new[]{ModeloNFe.NFe,ModeloNFe.NFCe})foreach(var uf in Enum.GetValues<OpenAC.Net.DFe.Core.Common.DFeSiglaUF>())
        {
            if(!Enum.IsDefined(typeof(OpenAC.Net.DFe.Core.Common.DFeCodUF),uf.ToString()) || uf.ToString() is "AN" or "EX" or "SU")continue;
            var c=new NFeConfig();c.Geral.Modelo=model;c.WebServices.UF=uf;
            await Assert.That(new Uri(NFeServiceManager.ObterEndereco(c,TipoServicoNFe.NFeAutorizacao)).Scheme).IsEqualTo("https");
        }
    }
}