using TUnit.Assertions.Enums;
using System.IO;
using NotaFiscal = OpenAC.Net.NFe.Common.Model.NFe;
using OpenAC.Net.NFe.Common;
using OpenAC.Net.NFe.Common.Types;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using OpenAC.Net.DFe.Core;
using OpenAC.Net.NFe.DANFe.EscPos;
using OpenAC.Net.NFe.DANFe.PDFSharp;
using PdfSharp.Pdf.IO;
using System.Threading.Tasks;

namespace OpenAC.Net.NFe.Tests;

public sealed class DocumentTests
{
    [Test]
    [Arguments(55,"4.00")][Arguments(65,"4.00")][Arguments(55,"3.10")][Arguments(65,"3.10")]
    public async Task AssinaValidaEImprimeVersoes(int modelo,string versao)
    {
        using var cert=Fixtures.Certificate();var nota=Fixtures.Nota(modelo,versao);nota.Assinar(cert);
        if(modelo==65&&versao=="4.00")nota.GerarQrCode(Fixtures.Config(cert,65));
        await Assert.That(nota.ValidarAssinatura()).IsTrue(); nota.ValidarIdentificacao();nota.Validar(Path.Combine(AppContext.BaseDirectory,"Schemas"));
        var protocolada=nota.ComProtocolo(Fixtures.Protocolo(nota));protocolada.Validar(Path.Combine(AppContext.BaseDirectory,"Schemas"));
        await Assert.That(protocolada.ValidarAssinatura()).IsTrue(); await Assert.That(protocolada.Autorizado).IsTrue();
        var pdf=new OpenDANFe().GerarPDF(protocolada);await Assert.That(Encoding.ASCII.GetString(pdf,0,8)).StartsWith("%PDF-");
        var dir=Path.Combine(AppContext.BaseDirectory,"Artifacts");Directory.CreateDirectory(dir);
        File.WriteAllBytes(Path.Combine(dir,$"danfe-{modelo}-{versao}.pdf"),pdf);
        nota.Save(Path.Combine(dir,$"nfe-{modelo}-{versao}.xml"));
    }
    [Test][Arguments("4.00")][Arguments("3.10")]
    public async Task ModelosDFeCorePreservamDadosEValidamNoSchema(string version)
    {
        using var cert=Fixtures.Certificate();var nota=Fixtures.Nota(55,version);
        var xml=NotaFiscal.Load(nota.GetXml()).GetXml();
        var typed=NotaFiscal.Load(xml);typed.Assinar(cert); typed.Validar(Path.Combine(AppContext.BaseDirectory,"Schemas"));
        await Assert.That(typed.Chave).IsEqualTo(nota.Chave);
        await Assert.That(typed.InfNFe.Total.ICMSTot.VNF).IsEqualTo(10m);
    }
    [Test]public async Task RecusaProtocoloDeOutraChaveOuDigest()
    {
        using var cert=Fixtures.Certificate();var nota=Fixtures.Nota();nota.Assinar(cert);var p=Fixtures.Protocolo(nota);
        p.InfProt.ChNFe=new string('1',44);
        await Assert.That(()=>nota.ComProtocolo(p)).ThrowsExactly<OpenDFeValidationException>();
        p =Fixtures.Protocolo(nota);p.InfProt.DigVal="OUTRO";
        await Assert.That(()=>nota.ComProtocolo(p)).ThrowsExactly<OpenDFeValidationException>();
    }
    [Test]public async Task AlteracaoNoModeloInvalidaAssinatura()
    {
        using var cert=Fixtures.Certificate();var nota=Fixtures.Nota();nota.Assinar(cert);var before=nota.GetXml();
        nota.InfNFe.Ide.NNF=123;
        await Assert.That(nota.GetXml()).IsNotEqualTo(before); await Assert.That(nota.ValidarAssinatura()).IsFalse();
        var adulterada=before.Replace("VENDA DE MERCADORIA","OPERACAO ALTERADA");await Assert.That(NotaFiscal.Load(adulterada).ValidarAssinatura()).IsFalse();
    }
    [Test]public async Task CarregamentoDoCoreNaoResolveEntidadesExternas()
    {
        var nota = NotaFiscal.Load("<!DOCTYPE NFe [<!ENTITY x SYSTEM 'file:///etc/passwd'>]><NFe xmlns='http://www.portalfiscal.inf.br/nfe'><infNFe versao='4.00'><ide><natOp>&x;</natOp></ide></infNFe></NFe>");
        await Assert.That(string.IsNullOrEmpty(nota.InfNFe.Ide.NatOp)).IsTrue();
    }
    [Test]public async Task QrCode2ConfereHashEIdCscSemZeros()
    {
        using var cert=Fixtures.Certificate();var nota=Fixtures.Nota(65);nota.Assinar(cert);var config=Fixtures.Config(cert,65);
        config.Geral.VersaoQRCode=NFeVersaoQRCode.v200;config.Geral.CSC="TOKEN-TESTE";config.Geral.IdCSC="000001";
        var qr=NFCeQrCode.Gerar(nota,config);var input=nota.Chave+"|2|2|1";
        using var sha=SHA1.Create();var hash=BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(input+"TOKEN-TESTE"))).Replace("-","");
        await Assert.That(qr).EndsWith(input+"|"+hash);
    }
    [Test]public async Task QrCode3NormalNaoUsaCsc()
    {
        using var cert=Fixtures.Certificate();var nota=Fixtures.Nota(65);nota.Assinar(cert);var config=Fixtures.Config(cert,65);
        await Assert.That(NFCeQrCode.Gerar(nota,config)).EndsWith(nota.Chave+"|3|2");
    }
    [Test][Arguments(2)][Arguments(3)]public async Task QrCodeOfflinePreservaAssinatura(int version)
    {
        using var cert=Fixtures.Certificate();var nota=Fixtures.Nota(65,offline:true);nota.Assinar(cert);var config=Fixtures.Config(cert,65);
        config.Geral.VersaoQRCode=(NFeVersaoQRCode)version;config.Geral.CSC="TOKEN";config.Geral.IdCSC="1";
        nota.GerarQrCode(config); await Assert.That(nota.QrCode).Contains("|28|10.00|"); await Assert.That(nota.ValidarAssinatura()).IsTrue(); nota.ValidarIdentificacao();nota.Validar(config.Arquivos.PathSchemas);
        if(version==3)
        {
            var parameters=nota.QrCode[(nota.QrCode.IndexOf("?p=",StringComparison.Ordinal)+3)..].Split('|');
            using var rsa=cert.GetRSAPublicKey();
            await Assert.That(rsa.VerifyData(Encoding.UTF8.GetBytes(string.Join("|",parameters.Take(7))),Convert.FromBase64String(Uri.UnescapeDataString(parameters[7])),HashAlgorithmName.SHA1,RSASignaturePadding.Pkcs1)).IsTrue();
        }
    }
    [Test]public async Task EscPosContemQrCodeCorteEPagamentos()
    {
        using var cert=Fixtures.Certificate();var nota=Fixtures.Nota(65);nota.Assinar(cert);nota.GerarQrCode(Fixtures.Config(cert,65));var printer=new OpenDANFeEscPos();
        var bytes=printer.Gerar(nota);await Assert.That(Encoding.GetEncoding(850).GetString(bytes)).Contains("\u001b@"); await Assert.That(bytes.TakeLast(3)).IsEquivalentTo(new byte[]{29,86,0}, CollectionOrdering.Matching);
        await Assert.That(printer.GerarTexto(nota)).Contains("Dinheiro"); await Assert.That(Encoding.UTF8.GetString(bytes)).Contains(nota.QrCode);
        await Assert.That(()=>printer.Gerar(Fixtures.Nota())).ThrowsExactly<ArgumentException>();
        var dir=Path.Combine(AppContext.BaseDirectory,"Artifacts");Directory.CreateDirectory(dir);File.WriteAllBytes(Path.Combine(dir,"nfce-escpos.bin"),bytes);
    }
    [Test][Arguments(58)][Arguments(80)]public async Task PdfTermicoRespeitaLargura(int width)
    {
        var pdf=new OpenDANFe();pdf.Configuracoes.LarguraNFCeMilimetros=width;var bytes=pdf.GerarPDF(Fixtures.Nota(65));
        using var doc=PdfReader.Open(new MemoryStream(bytes),PdfDocumentOpenMode.Import);await Assert.That(doc.Pages[0].Width.Point).IsBetween(width*72d/25.4-0.1,width*72d/25.4+0.1).Inclusive();
    }
    [Test]public async Task MuitosProdutosPaginamSemPerda()
    {
        var bytes=new OpenDANFe().GerarPDF(Fixtures.Nota(itens:150));using var doc=PdfReader.Open(new MemoryStream(bytes),PdfDocumentOpenMode.Import);await Assert.That(doc.PageCount>2).IsTrue();
        var dir=Path.Combine(AppContext.BaseDirectory,"Artifacts");Directory.CreateDirectory(dir);File.WriteAllBytes(Path.Combine(dir,"danfe-multipagina.pdf"),bytes);
    }
}