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
using Xunit;

namespace OpenAC.Net.NFe.Tests;

public sealed class DocumentTests
{
    [Theory]
    [InlineData(55,"4.00")][InlineData(65,"4.00")][InlineData(55,"3.10")][InlineData(65,"3.10")]
    public void AssinaValidaEImprimeVersoes(int modelo,string versao)
    {
        using var cert=Fixtures.Certificate();var nota=Fixtures.Nota(modelo,versao);nota.Assinar(cert);
        if(modelo==65&&versao=="4.00")nota.GerarQrCode(Fixtures.Config(cert,65));
        Assert.True(nota.ValidarAssinatura());nota.ValidarIdentificacao();nota.Validar(Path.Combine(AppContext.BaseDirectory,"Schemas"));
        var protocolada=nota.ComProtocolo(Fixtures.Protocolo(nota));protocolada.Validar(Path.Combine(AppContext.BaseDirectory,"Schemas"));
        Assert.True(protocolada.ValidarAssinatura()); Assert.True(protocolada.Autorizado);
        var pdf=new OpenDANFe().GerarPDF(protocolada);Assert.StartsWith("%PDF-",Encoding.ASCII.GetString(pdf,0,8));
        var dir=Path.Combine(AppContext.BaseDirectory,"Artifacts");Directory.CreateDirectory(dir);
        File.WriteAllBytes(Path.Combine(dir,$"danfe-{modelo}-{versao}.pdf"),pdf);
        nota.Save(Path.Combine(dir,$"nfe-{modelo}-{versao}.xml"));
    }
    [Theory][InlineData("4.00")][InlineData("3.10")]
    public void ModelosDFeCorePreservamDadosEValidamNoSchema(string version)
    {
        using var cert=Fixtures.Certificate();var nota=Fixtures.Nota(55,version);
        var xml=NotaFiscal.Load(nota.GetXml()).GetXml();
        var typed=NotaFiscal.Load(xml);typed.Assinar(cert); typed.Validar(Path.Combine(AppContext.BaseDirectory,"Schemas"));
        Assert.Equal(nota.Chave,typed.Chave);
        Assert.Equal(10m,typed.InfNFe.Total.ICMSTot.VNF);
    }
    [Fact]public void RecusaProtocoloDeOutraChaveOuDigest()
    {
        using var cert=Fixtures.Certificate();var nota=Fixtures.Nota();nota.Assinar(cert);var p=Fixtures.Protocolo(nota);
        p.InfProt.ChNFe=new string('1',44);
        Assert.Throws<OpenDFeValidationException>(()=>nota.ComProtocolo(p));
        p=Fixtures.Protocolo(nota);p.InfProt.DigVal="OUTRO";
        Assert.Throws<OpenDFeValidationException>(()=>nota.ComProtocolo(p));
    }
    [Fact]public void AlteracaoNoModeloInvalidaAssinatura()
    {
        using var cert=Fixtures.Certificate();var nota=Fixtures.Nota();nota.Assinar(cert);var before=nota.GetXml();
        nota.InfNFe.Ide.NNF=123;
        Assert.NotEqual(before,nota.GetXml());Assert.False(nota.ValidarAssinatura());
        var adulterada=before.Replace("VENDA DE MERCADORIA","OPERACAO ALTERADA");Assert.False(NotaFiscal.Load(adulterada).ValidarAssinatura());
    }
    [Fact]public void CarregamentoDoCoreNaoResolveEntidadesExternas()
    {
        var nota = NotaFiscal.Load("<!DOCTYPE NFe [<!ENTITY x SYSTEM 'file:///etc/passwd'>]><NFe xmlns='http://www.portalfiscal.inf.br/nfe'><infNFe versao='4.00'><ide><natOp>&x;</natOp></ide></infNFe></NFe>");
        Assert.True(string.IsNullOrEmpty(nota.InfNFe.Ide.NatOp));
    }
    [Fact]public void QrCode2ConfereHashEIdCscSemZeros()
    {
        using var cert=Fixtures.Certificate();var nota=Fixtures.Nota(65);nota.Assinar(cert);var config=Fixtures.Config(cert,65);
        config.Geral.VersaoQRCode=NFeVersaoQRCode.v200;config.Geral.CSC="TOKEN-TESTE";config.Geral.IdCSC="000001";
        var qr=NFCeQrCode.Gerar(nota,config);var input=nota.Chave+"|2|2|1";
        using var sha=SHA1.Create();var hash=BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(input+"TOKEN-TESTE"))).Replace("-","");
        Assert.EndsWith(input+"|"+hash,qr);
    }
    [Fact]public void QrCode3NormalNaoUsaCsc()
    {
        using var cert=Fixtures.Certificate();var nota=Fixtures.Nota(65);nota.Assinar(cert);var config=Fixtures.Config(cert,65);
        Assert.EndsWith(nota.Chave+"|3|2",NFCeQrCode.Gerar(nota,config));
    }
    [Theory][InlineData(2)][InlineData(3)]public void QrCodeOfflinePreservaAssinatura(int version)
    {
        using var cert=Fixtures.Certificate();var nota=Fixtures.Nota(65,offline:true);nota.Assinar(cert);var config=Fixtures.Config(cert,65);
        config.Geral.VersaoQRCode=(NFeVersaoQRCode)version;config.Geral.CSC="TOKEN";config.Geral.IdCSC="1";
        nota.GerarQrCode(config); Assert.Contains("|28|10.00|",nota.QrCode);Assert.True(nota.ValidarAssinatura());nota.ValidarIdentificacao();nota.Validar(config.Arquivos.PathSchemas);
        if(version==3)
        {
            var parameters=nota.QrCode[(nota.QrCode.IndexOf("?p=",StringComparison.Ordinal)+3)..].Split('|');
            using var rsa=cert.GetRSAPublicKey();
            Assert.True(rsa.VerifyData(Encoding.UTF8.GetBytes(string.Join("|",parameters.Take(7))),Convert.FromBase64String(Uri.UnescapeDataString(parameters[7])),HashAlgorithmName.SHA1,RSASignaturePadding.Pkcs1));
        }
    }
    [Fact]public void EscPosContemQrCodeCorteEPagamentos()
    {
        using var cert=Fixtures.Certificate();var nota=Fixtures.Nota(65);nota.Assinar(cert);nota.GerarQrCode(Fixtures.Config(cert,65));var printer=new OpenDANFeEscPos();
        var bytes=printer.Gerar(nota);Assert.Contains("\u001b@",Encoding.GetEncoding(850).GetString(bytes));Assert.Equal(new byte[]{29,86,0},bytes.TakeLast(3));
        Assert.Contains("Dinheiro",printer.GerarTexto(nota));Assert.Contains(nota.QrCode,Encoding.UTF8.GetString(bytes));
        Assert.Throws<ArgumentException>(()=>printer.Gerar(Fixtures.Nota()));
        var dir=Path.Combine(AppContext.BaseDirectory,"Artifacts");Directory.CreateDirectory(dir);File.WriteAllBytes(Path.Combine(dir,"nfce-escpos.bin"),bytes);
    }
    [Theory][InlineData(58)][InlineData(80)]public void PdfTermicoRespeitaLargura(int width)
    {
        var pdf=new OpenDANFe();pdf.Configuracoes.LarguraNFCeMilimetros=width;var bytes=pdf.GerarPDF(Fixtures.Nota(65));
        using var doc=PdfReader.Open(new MemoryStream(bytes),PdfDocumentOpenMode.Import);Assert.InRange(doc.Pages[0].Width.Point,width*72d/25.4-0.1,width*72d/25.4+0.1);
    }
    [Fact]public void MuitosProdutosPaginamSemPerda()
    {
        var bytes=new OpenDANFe().GerarPDF(Fixtures.Nota(itens:150));using var doc=PdfReader.Open(new MemoryStream(bytes),PdfDocumentOpenMode.Import);Assert.True(doc.PageCount>2);
        var dir=Path.Combine(AppContext.BaseDirectory,"Artifacts");Directory.CreateDirectory(dir);File.WriteAllBytes(Path.Combine(dir,"danfe-multipagina.pdf"),bytes);
    }
}
