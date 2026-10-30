using NotaFiscal = OpenAC.Net.NFe.Common.Model.NFe;
using OpenAC.Net.NFe.Common;
using OpenAC.Net.NFe.Common.Model;
using OpenAC.Net.NFe.Common.Types;
using OpenAC.Net.NFe.Certificates;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Xml.Linq;
using OpenAC.Net.DFe.Core.Http;
using System.Net;

namespace OpenAC.Net.NFe.Tests;

internal static class Fixtures
{
    static Fixtures() => System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
    internal static X509Certificate2 Certificate()
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest("CN=OpenAC Teste", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        return request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
    }
    internal static NFeConfig Config(X509Certificate2 cert, int model = 55) => new()
    { Certificado = cert, Geral = { Modelo = (ModeloNFe)model } };
    internal static OpenNFe Client(NFeConfig config, FakeTransport transport)
    {
        config.HttpTransport = transport;
        return new OpenNFe(config, new TestCertificateProvider(config.Certificado));
    }
    internal static NotaFiscal Nota(int model = 55, string version = "4.00", bool offline = false, int itens = 1)
    {
        var key = "";
        var det = string.Join("", Enumerable.Range(1, itens).Select(i => $"""
        <det nItem="{i}"><prod><cProd>{i:D4}</cProd><cEAN>SEM GTIN</cEAN><xProd>PRODUTO DE TESTE - SEM VALOR FISCAL {i}</xProd><NCM>22021000</NCM><CFOP>5102</CFOP><uCom>UN</uCom><qCom>1.0000</qCom><vUnCom>10.0000000000</vUnCom><vProd>10.00</vProd><cEANTrib>SEM GTIN</cEANTrib><uTrib>UN</uTrib><qTrib>1.0000</qTrib><vUnTrib>10.0000000000</vUnTrib><indTot>1</indTot></prod><imposto><vTotTrib>1.80</vTotTrib><ICMS><ICMS00><orig>0</orig><CST>00</CST><modBC>3</modBC><vBC>10.00</vBC><pICMS>18.0000</pICMS><vICMS>1.80</vICMS></ICMS00></ICMS><PIS><PISNT><CST>07</CST></PISNT></PIS><COFINS><COFINSNT><CST>07</CST></COFINSNT></COFINS></imposto></det>
        """));
        var xml = $"""
        <NFe xmlns="http://www.portalfiscal.inf.br/nfe"><infNFe versao="{version}" Id="{key}">
        <ide><cUF>35</cUF><cNF>12345678</cNF><natOp>VENDA DE MERCADORIA</natOp><mod>{model}</mod><serie>1</serie><nNF>1</nNF><dhEmi>2026-10-28T10:00:00-03:00</dhEmi><tpNF>1</tpNF><idDest>1</idDest><cMunFG>3550308</cMunFG><tpImp>{(model==65?4:1)}</tpImp><tpEmis>{(offline?9:1)}</tpEmis><cDV>0</cDV><tpAmb>2</tpAmb><finNFe>1</finNFe><indFinal>1</indFinal><indPres>1</indPres><procEmi>0</procEmi><verProc>OpenAC 1.0</verProc>{(offline?"<dhCont>2026-10-28T10:00:00-03:00</dhCont><xJust>Falha de comunicacao com o autorizador</xJust>":"")}</ide>
        <emit><CNPJ>12345678000195</CNPJ><xNome>OPENAC COMERCIO DE TESTE LTDA</xNome><enderEmit><xLgr>Rua de Teste</xLgr><nro>100</nro><xBairro>Centro</xBairro><cMun>3550308</cMun><xMun>Sao Paulo</xMun><UF>SP</UF><CEP>01001000</CEP><cPais>1058</cPais><xPais>BRASIL</xPais></enderEmit><IE>123456789012</IE><CRT>3</CRT></emit>
        <dest><CNPJ>98765432000198</CNPJ><xNome>NF-E EMITIDA EM AMBIENTE DE HOMOLOGACAO - SEM VALOR FISCAL</xNome><enderDest><xLgr>Rua do Cliente</xLgr><nro>200</nro><xBairro>Centro</xBairro><cMun>3550308</cMun><xMun>Sao Paulo</xMun><UF>SP</UF><CEP>01001000</CEP></enderDest><indIEDest>9</indIEDest></dest>
        {det}
        <total><ICMSTot><vBC>{itens*10}.00</vBC><vICMS>{(itens*1.8m).ToString("F2",System.Globalization.CultureInfo.InvariantCulture)}</vICMS><vICMSDeson>0.00</vICMSDeson><vFCP>0.00</vFCP><vBCST>0.00</vBCST><vST>0.00</vST><vFCPST>0.00</vFCPST><vFCPSTRet>0.00</vFCPSTRet><vProd>{itens*10}.00</vProd><vFrete>0.00</vFrete><vSeg>0.00</vSeg><vDesc>0.00</vDesc><vII>0.00</vII><vIPI>0.00</vIPI><vIPIDevol>0.00</vIPIDevol><vPIS>0.00</vPIS><vCOFINS>0.00</vCOFINS><vOutro>0.00</vOutro><vNF>{itens*10}.00</vNF><vTotTrib>1.80</vTotTrib></ICMSTot></total>
        <transp><modFrete>9</modFrete></transp><pag><detPag><tPag>01</tPag><vPag>{itens*10}.00</vPag></detPag><vTroco>0.00</vTroco></pag><infAdic><infCpl>DOCUMENTO DE EXEMPLO PARA TESTES. NAO TRANSMITIR.</infCpl></infAdic></infNFe></NFe>
        """;
        var element = XmlFixture.Parse(xml);
        if (version == "3.10")
        {
            var info = element.Element(XmlFixture.Ns + "infNFe");
            foreach(var ean in info.Descendants().Where(e => e.Name.LocalName is "cEAN" or "cEANTrib")) ean.Value = "7891234567895";
            info.Element(XmlFixture.Ns + "ide").Element(XmlFixture.Ns + "natOp").AddAfterSelf(new XElement(XmlFixture.Ns + "indPag", "0"));
            foreach (var name in new[] {"vFCP","vFCPST","vFCPSTRet","vIPIDevol"}) info.Element(XmlFixture.Ns+"total").Element(XmlFixture.Ns+"ICMSTot").Element(XmlFixture.Ns+name)?.Remove();
            var pag=info.Element(XmlFixture.Ns+"pag"); pag.ReplaceWith(new XElement(XmlFixture.Ns+"pag",pag.Element(XmlFixture.Ns+"detPag").Elements()));
        }
        var nota=NotaFiscal.Load(element.ToString(SaveOptions.DisableFormatting)); nota.GerarChave(); return nota;
    }
    internal static NFeProtNFe Protocolo(NotaFiscal nota, string status = "100") => new()
    {
        Versao = nota.InfNFe.Versao,
        InfProt = new NFeInfProt
        {
            TpAmb = "2", VerAplic = "TESTE1.0", ChNFe = nota.Chave,
            DhRecbto = "2026-10-28T10:01:00-03:00", NProt = "135260000000001",
            DigVal = nota.Signature.SignedInfo.Reference.DigestValue,
            CStat = int.Parse(status), XMotivo = "Autorizado o uso da NF-e"
        }
    };

}
internal sealed class FakeTransport : IDFeHttpTransport
{
    internal int Calls;
    internal CapturedRequest Request;
    internal Func<CapturedRequest, string> Response = _ => "<retConsStatServ xmlns='http://www.portalfiscal.inf.br/nfe'><cStat>107</cStat><xMotivo>Servico em Operacao</xMotivo></retConsStatServ>";
    public async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, DFeHttpTransportOptions options, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Interlocked.Increment(ref Calls);
        Request = new CapturedRequest(request.RequestUri!, request.Content!.Headers.ContentType!.Parameters.Single(value => value.Name == "action").Value!.Trim('"'), await request.Content.ReadAsStringAsync(cancellationToken));
        var payload = Response(Request);
        var envelope = "<s:Envelope xmlns:s='http://www.w3.org/2003/05/soap-envelope'><s:Body>" + payload + "</s:Body></s:Envelope>";
        return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(envelope) };
    }
}
internal sealed record CapturedRequest(Uri Endereco, string Action, string Envelope);
internal sealed class TestCertificateProvider(X509Certificate2 certificate = null) : INFeCertificadoProvider
{
    public Task<X509Certificate2> ObterAsync(NFeConfigCertificados config, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(certificate == null ? Fixtures.Certificate() : new X509Certificate2(certificate));
    }
}
