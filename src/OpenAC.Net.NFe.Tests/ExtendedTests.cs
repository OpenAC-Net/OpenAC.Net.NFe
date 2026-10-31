using NotaFiscal = OpenAC.Net.NFe.Common.Model.NFe;
using OpenAC.Net.NFe.Common;
using OpenAC.Net.NFe.Common.Model;
using Microsoft.Extensions.DependencyInjection;
using OpenAC.Net.DFe.Core;
using OpenAC.Net.NFe.Web;
using System.Threading.Tasks;
using System.Security.Cryptography.X509Certificates;

namespace OpenAC.Net.NFe.Tests;

public sealed class ExtendedTests
{
    [Test]public async Task InutilizacaoAssinaEValidaEnvelope()
    {
        using var cert=Fixtures.Certificate();var t=new FakeTransport{Response=_=>"<retInutNFe xmlns='http://www.portalfiscal.inf.br/nfe'><cStat>102</cStat></retInutNFe>"};
        var c=Fixtures.Client(Fixtures.Config(cert),t);await Assert.That((await c.InutilizarAsync(new NFeInutilizacaoSolicitacao { Cnpj = "12345678000195", Ano = 26, Serie = 1, NumeroInicial = 1, NumeroFinal = 3, Justificativa = "Inutilizacao de numeracao para teste" })).CStat).IsEqualTo(102);
        await Assert.That(t.Request.Envelope).Contains("Signature");
    }
    [Test]public async Task ConsultaCadastroValidaSchemaEAction()
    {
        var t=new FakeTransport{Response=_=>"<retConsCad xmlns='http://www.portalfiscal.inf.br/nfe'><infCons><cStat>111</cStat></infCons></retConsCad>"};
        var c=Fixtures.Client(new NFeConfig(),t);await c.ConsultaCadastroAsync("12345678000195");await Assert.That(t.Request.Action).EndsWith("CadConsultaCadastro4/consultaCadastro");
    }
    [Test]
    public async Task DistribuicaoUsaAmbienteNacionalEWrapperCorreto()
    {
        var transporte = new FakeTransport
        {
            Response = _ => "<retDistDFeInt xmlns='http://www.portalfiscal.inf.br/nfe'><cStat>137</cStat></retDistDFeInt>"
        };
        var cliente = Fixtures.Client(new NFeConfig(), transporte);
        var resposta = await cliente.ConsultarDistribuicaoAsync(new NFeDistribuicaoSolicitacao
        { CpfCnpj = "12345678000195", UltimoNsu = "000000000000000" });
        await Assert.That(resposta.CStat).IsEqualTo(137);
        await Assert.That(transporte.Request.Endereco.Host).IsEqualTo("hom1.nfe.fazenda.gov.br");
        var envelope = System.Xml.Linq.XElement.Parse(transporte.Request.Envelope);
        System.Xml.Linq.XNamespace soap = "http://www.w3.org/2003/05/soap-envelope";
        System.Xml.Linq.XNamespace contrato = "http://www.portalfiscal.inf.br/nfe/wsdl/NFeDistribuicaoDFe";
        var operacao = await Assert.That(envelope.Element(soap + "Body")!.Elements()).HasSingleItem();
        await Assert.That(operacao.Name).IsEqualTo(contrato + "nfeDistDFeInteresse");
        var mensagem = await Assert.That(operacao.Elements()).HasSingleItem();
        await Assert.That(mensagem.Name).IsEqualTo(contrato + "nfeDadosMsg");
        await Assert.That((await Assert.That(mensagem.Elements()).HasSingleItem()).Name).IsEqualTo(XmlFixture.Ns + "distDFeInt");
        await Assert.That(resposta.XmlEnvio).DoesNotContain("<?xml");
        await Assert.That(transporte.Request.Envelope).Contains(resposta.XmlEnvio);
    }
    [Test]public async Task ReciboPendenteNaoAutorizaDocumento()
    {
        var t=new FakeTransport{Response=_=>"<retConsReciNFe xmlns='http://www.portalfiscal.inf.br/nfe'><cStat>105</cStat><xMotivo>Lote em processamento</xMotivo></retConsReciNFe>"};
        var c=Fixtures.Client(new NFeConfig(),t);var result=await c.ConsultaReciboAsync(new NFeReciboSolicitacao { Recibo = "351260000000001" });await Assert.That(result.CStat).IsEqualTo(105); await Assert.That(result.Documentos).IsEmpty(); await Assert.That(t.Calls).IsEqualTo(1);
    }
    [Test][Arguments(210200)][Arguments(210210)][Arguments(210220)][Arguments(210240)]public async Task ManifestacaoValidaEDirecionaANacional(int code)
    {
        using var cert=Fixtures.Certificate();var nota=Fixtures.Nota();nota.Assinar(cert);var t=new FakeTransport{Response=_=>"<retEnvEvento xmlns='http://www.portalfiscal.inf.br/nfe'><cStat>128</cStat></retEnvEvento>"};
        var c=Fixtures.Client(Fixtures.Config(cert),t);await c.EnviarEventoAsync(new NFeEventoSolicitacao { Eventos =
            [NFeEvento.Manifestacao(nota.Chave,"12345678000195",code,"Operacao nao realizada por erro do fornecedor")]
        });
        await Assert.That(t.Request.Action).EndsWith("NFeRecepcaoEvento4/nfeRecepcaoEvento"); await Assert.That(t.Request.Envelope).Contains("<cOrgao>91</cOrgao>");
    }
    [Test]public async Task CartaCorrecaoESubstituicaoValidamDetalhes()
    {
        using var cert=Fixtures.Certificate();var n=Fixtures.Nota();NFeEvento.CartaCorrecao(n.Chave,"12345678000195",1,"Correcao do endereco de entrega da mercadoria").Assinar(Fixtures.Config(cert));
        var nfce=Fixtures.Nota(65);NFeEvento.CancelamentoSubstituicao(nfce.Chave,"12345678000195","135260000000001",nfce.Chave,"Cancelamento por emissao em duplicidade").Assinar(Fixtures.Config(cert,65));
    }
    [Test]public async Task XmlComChaveDivergenteNaoPodeSerAssinado()
    {
        using var cert=Fixtures.Certificate();var nota=Fixtures.Nota();var adulterada=nota.GetXml().Replace("<nNF>1</nNF>","<nNF>2</nNF>");await Assert.That(()=>NotaFiscal.Load(adulterada).Assinar(cert)).ThrowsExactly<OpenDFeValidationException>();
    }
    [Test]public async Task CnpjAlfanumericoGeraChaveAssinaEImprime()
    {
        using var cert=Fixtures.Certificate();var xml=XmlFixture.Parse(Fixtures.Nota().GetXml());xml.Element(XmlFixture.Ns+"infNFe").Element(XmlFixture.Ns+"emit").Element(XmlFixture.Ns+"CNPJ").Value="12ABC345000A92";
        var n=NotaFiscal.Load(xml.ToString());n.GerarChave();await Assert.That(n.Chave).Contains("12ABC345000A92"); n.Assinar(cert);n.Validar(AppContext.BaseDirectory+"/Schemas");await Assert.That(n.ValidarAssinatura()).IsTrue();
        await Assert.That(new DANFe.PDFSharp.OpenDANFe().GerarPDF(n).Length>1000).IsTrue();
    }
    [Test]public async Task ModelosPreservamIBSCBSEValoresOpcionaisZerados()
    {
        using var cert=Fixtures.Certificate();var xml=XmlFixture.Parse(Fixtures.Nota().GetXml());
        xml.Descendants(XmlFixture.Ns+"imposto").Single().Add(XmlFixture.Parse("""
        <IBSCBS xmlns="http://www.portalfiscal.inf.br/nfe"><CST>000</CST><cClassTrib>000001</cClassTrib><indDoacao>1</indDoacao><gIBSCBS><vBC>10.00</vBC><gIBSUF><pIBSUF>0.1000</pIBSUF><vIBSUF>0.01</vIBSUF></gIBSUF><gIBSMun><pIBSMun>0.0000</pIBSMun><vIBSMun>0.00</vIBSMun></gIBSMun><vIBS>0.01</vIBS><gCBS><pCBS>0.9000</pCBS><vCBS>0.09</vCBS></gCBS></gIBSCBS></IBSCBS>
        """));
        xml.Descendants(XmlFixture.Ns+"imposto").Single().Element(XmlFixture.Ns+"vTotTrib").Value="0.00";
        var modelo=NotaFiscal.Load(xml.ToString());var n=modelo;n.Assinar(cert);n.Validar(AppContext.BaseDirectory+"/Schemas");
        await Assert.That(n.GetXml()).Contains("<vTotTrib>0.00</vTotTrib>"); await Assert.That(n.GetXml()).Contains("<vCBS>0.09</vCBS>");
    }
    [Test]public async Task FactoryWebCriaClientesIndependentesPorSessao()
    {
        using var cert=Fixtures.Certificate();var config=Fixtures.Config(cert);
        var services=new ServiceCollection();services.AddNFe(c=>{c.Certificados.CertificadoBytes=cert.Export(X509ContentType.Pfx, "test");c.Certificados.Senha="test";c.Geral.CSC="ORIGINAL";});
        using var provider=services.BuildServiceProvider();using var scope=provider.CreateScope();var factory=scope.ServiceProvider.GetRequiredService<IOpenNFeClientFactory>();
        var a=await factory.CriarAsync("empresa-a");var b=await factory.CriarAsync("empresa-b");
        await Assert.That(b).IsNotSameReferenceAs(a);
        await Assert.That(a).IsAssignableTo<IOpenNFeClient>();
        await Assert.That(cert.HasPrivateKey).IsTrue();
        await Assert.That(() => b.ConsultarAsync("chave-invalida")).ThrowsExactly<ArgumentException>();
    }
}
