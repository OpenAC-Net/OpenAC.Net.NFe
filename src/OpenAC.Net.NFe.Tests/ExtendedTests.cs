using NotaFiscal = OpenAC.Net.NFe.Common.Model.NFe;
using OpenAC.Net.NFe.Common;
using OpenAC.Net.NFe.Common.Model;
using Microsoft.Extensions.DependencyInjection;
using OpenAC.Net.DFe.Core;
using OpenAC.Net.NFe.Web;
using Xunit;

namespace OpenAC.Net.NFe.Tests;

public sealed class ExtendedTests
{
    [Fact]public async Task InutilizacaoAssinaEValidaEnvelope()
    {
        using var cert=Fixtures.Certificate();var t=new FakeTransport{Response=_=>"<retInutNFe xmlns='http://www.portalfiscal.inf.br/nfe'><cStat>102</cStat></retInutNFe>"};
        var c=Fixtures.Client(Fixtures.Config(cert),t);Assert.Equal(102,(await c.InutilizarAsync(new NFeInutilizacaoSolicitacao { Cnpj = "12345678000195", Ano = 26, Serie = 1, NumeroInicial = 1, NumeroFinal = 3, Justificativa = "Inutilizacao de numeracao para teste" })).CStat);
        Assert.Contains("Signature",t.Request.Envelope);
    }
    [Fact]public async Task ConsultaCadastroValidaSchemaEAction()
    {
        var t=new FakeTransport{Response=_=>"<retConsCad xmlns='http://www.portalfiscal.inf.br/nfe'><infCons><cStat>111</cStat></infCons></retConsCad>"};
        var c=Fixtures.Client(new NFeConfig(),t);await c.ConsultaCadastroAsync("12345678000195");Assert.EndsWith("CadConsultaCadastro4/consultaCadastro",t.Request.Action);
    }
    [Fact]
    public async Task DistribuicaoUsaAmbienteNacionalEWrapperCorreto()
    {
        var transporte = new FakeTransport
        {
            Response = _ => "<retDistDFeInt xmlns='http://www.portalfiscal.inf.br/nfe'><cStat>137</cStat></retDistDFeInt>"
        };
        var cliente = Fixtures.Client(new NFeConfig(), transporte);
        var resposta = await cliente.DistribuicaoDFeAsync(new NFeDistribuicaoSolicitacao
        { CpfCnpj = "12345678000195", UltimoNsu = "000000000000000" });
        Assert.Equal(137, resposta.CStat);
        Assert.Equal("hom1.nfe.fazenda.gov.br", transporte.Request.Endereco.Host);
        var envelope = System.Xml.Linq.XElement.Parse(transporte.Request.Envelope);
        System.Xml.Linq.XNamespace soap = "http://www.w3.org/2003/05/soap-envelope";
        System.Xml.Linq.XNamespace contrato = "http://www.portalfiscal.inf.br/nfe/wsdl/NFeDistribuicaoDFe";
        var operacao = Assert.Single(envelope.Element(soap + "Body")!.Elements());
        Assert.Equal(contrato + "nfeDistDFeInteresse", operacao.Name);
        var mensagem = Assert.Single(operacao.Elements());
        Assert.Equal(contrato + "nfeDadosMsg", mensagem.Name);
        Assert.Equal(XmlFixture.Ns + "distDFeInt", Assert.Single(mensagem.Elements()).Name);
        Assert.DoesNotContain("<?xml", resposta.XmlEnvio);
        Assert.Contains(resposta.XmlEnvio, transporte.Request.Envelope, StringComparison.Ordinal);
    }
    [Fact]public async Task ReciboPendenteNaoAutorizaDocumento()
    {
        var t=new FakeTransport{Response=_=>"<retConsReciNFe xmlns='http://www.portalfiscal.inf.br/nfe'><cStat>105</cStat><xMotivo>Lote em processamento</xMotivo></retConsReciNFe>"};
        var c=Fixtures.Client(new NFeConfig(),t);var result=await c.ConsultaReciboAsync(new NFeReciboSolicitacao { Recibo = "351260000000001" });Assert.Equal(105,result.CStat);Assert.Empty(result.Documentos);Assert.Equal(1,t.Calls);
    }
    [Theory][InlineData(210200)][InlineData(210210)][InlineData(210220)][InlineData(210240)]public async Task ManifestacaoValidaEDirecionaANacional(int code)
    {
        using var cert=Fixtures.Certificate();var nota=Fixtures.Nota();nota.Assinar(cert);var t=new FakeTransport{Response=_=>"<retEnvEvento xmlns='http://www.portalfiscal.inf.br/nfe'><cStat>128</cStat></retEnvEvento>"};
        var c=Fixtures.Client(Fixtures.Config(cert),t);await c.EnviarEventoAsync(new NFeEventoSolicitacao { Eventos =
            [NFeEvento.Manifestacao(nota.Chave,"12345678000195",code,"Operacao nao realizada por erro do fornecedor")]
        });
        Assert.EndsWith("NFeRecepcaoEvento4/nfeRecepcaoEvento",t.Request.Action);Assert.Contains("<cOrgao>91</cOrgao>",t.Request.Envelope);
    }
    [Fact]public void CartaCorrecaoESubstituicaoValidamDetalhes()
    {
        using var cert=Fixtures.Certificate();var n=Fixtures.Nota();NFeEvento.CartaCorrecao(n.Chave,"12345678000195",1,"Correcao do endereco de entrega da mercadoria").Assinar(Fixtures.Config(cert));
        var nfce=Fixtures.Nota(65);NFeEvento.CancelamentoSubstituicao(nfce.Chave,"12345678000195","135260000000001",nfce.Chave,"Cancelamento por emissao em duplicidade").Assinar(Fixtures.Config(cert,65));
    }
    [Fact]public void XmlComChaveDivergenteNaoPodeSerAssinado()
    {
        using var cert=Fixtures.Certificate();var nota=Fixtures.Nota();var adulterada=nota.GetXml().Replace("<nNF>1</nNF>","<nNF>2</nNF>");Assert.Throws<OpenDFeValidationException>(()=>NotaFiscal.Load(adulterada).Assinar(cert));
    }
    [Fact]public void CnpjAlfanumericoGeraChaveAssinaEImprime()
    {
        using var cert=Fixtures.Certificate();var xml=XmlFixture.Parse(Fixtures.Nota().GetXml());xml.Element(XmlFixture.Ns+"infNFe").Element(XmlFixture.Ns+"emit").Element(XmlFixture.Ns+"CNPJ").Value="12ABC345000A92";
        var n=NotaFiscal.Load(xml.ToString());n.GerarChave();Assert.Contains("12ABC345000A92",n.Chave);n.Assinar(cert);n.Validar(AppContext.BaseDirectory+"/Schemas");Assert.True(n.ValidarAssinatura());
        Assert.True(new DANFe.PDFSharp.OpenDANFe().GerarPDF(n).Length>1000);
    }
    [Fact]public void ModelosPreservamIBSCBSEValoresOpcionaisZerados()
    {
        using var cert=Fixtures.Certificate();var xml=XmlFixture.Parse(Fixtures.Nota().GetXml());
        xml.Descendants(XmlFixture.Ns+"imposto").Single().Add(XmlFixture.Parse("""
        <IBSCBS xmlns="http://www.portalfiscal.inf.br/nfe"><CST>000</CST><cClassTrib>000001</cClassTrib><indDoacao>1</indDoacao><gIBSCBS><vBC>10.00</vBC><gIBSUF><pIBSUF>0.1000</pIBSUF><vIBSUF>0.01</vIBSUF></gIBSUF><gIBSMun><pIBSMun>0.0000</pIBSMun><vIBSMun>0.00</vIBSMun></gIBSMun><vIBS>0.01</vIBS><gCBS><pCBS>0.9000</pCBS><vCBS>0.09</vCBS></gCBS></gIBSCBS></IBSCBS>
        """));
        xml.Descendants(XmlFixture.Ns+"imposto").Single().Element(XmlFixture.Ns+"vTotTrib").Value="0.00";
        var modelo=NotaFiscal.Load(xml.ToString());var n=modelo;n.Assinar(cert);n.Validar(AppContext.BaseDirectory+"/Schemas");
        Assert.Contains("<vTotTrib>0.00</vTotTrib>",n.GetXml());Assert.Contains("<vCBS>0.09</vCBS>",n.GetXml());
    }
    [Fact]public async Task FactoryWebCriaClientesIndependentesPorSessao()
    {
        using var cert=Fixtures.Certificate();var config=Fixtures.Config(cert);
        var services=new ServiceCollection();services.AddNFe(c=>{c.Certificado=cert;c.Geral.CSC="ORIGINAL";});
        using var provider=services.BuildServiceProvider();using var scope=provider.CreateScope();var factory=scope.ServiceProvider.GetRequiredService<IOpenNFeClientFactory>();
        using var a=await factory.CreateAsync("empresa-a");using var b=await factory.CreateAsync("empresa-b");
        Assert.NotSame(a.Client,b.Client);
        Assert.IsAssignableFrom<IOpenNFeClient>(a.Client);
        a.Dispose();
        Assert.True(cert.HasPrivateKey);
        await Assert.ThrowsAsync<ArgumentException>(() => b.Client.ConsultarAsync("chave-invalida"));
    }
}
