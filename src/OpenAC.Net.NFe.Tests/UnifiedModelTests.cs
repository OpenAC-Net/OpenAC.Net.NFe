using NotaFiscal = OpenAC.Net.NFe.Common.Model.NFe;
using OpenAC.Net.NFe.Common.Types;
using System.Xml.Linq;
using OpenAC.Net.DFe.Core;
using Xunit;

namespace OpenAC.Net.NFe.Tests;

public sealed class UnifiedModelTests
{
    [Theory]
    [InlineData(55, "3.10", NFeVersao.v310)]
    [InlineData(65, "3.10", NFeVersao.v310)]
    [InlineData(55, "4.00", NFeVersao.v400)]
    [InlineData(65, "4.00", NFeVersao.v400)]
    public void MesmoModeloLeAssinaEValidaOsDoisLeiautes(int model, string version, NFeVersao expected)
    {
        using var certificate = Fixtures.Certificate();
        var source = Fixtures.Nota(model, version);
        var typed = NotaFiscal.Load(source.GetXml());
        Assert.Equal(expected, typed.InfNFe.Versao);
        Assert.Same(typed.InfNFe, typed.InfNFe.Ide.Parent);
        Assert.Same(typed.InfNFe, typed.InfNFe.Det[0].Prod.Parent);
        Assert.Same(typed.InfNFe, typed.InfNFe.Det[0].Imposto.ICMS.Parent);
        Assert.Equal(source.Chave, typed.InfNFe.Id[3..]);
        var document = typed;
        document.Assinar(certificate);
        if (model == 65 && version == "4.00") document.GerarQrCode(Fixtures.Config(certificate, model));
        document.Validar(Path.Combine(AppContext.BaseDirectory, "Schemas"));
        Assert.True(document.ValidarAssinatura());
        Assert.Equal(expected, document.InfNFe.Versao);
    }

    [Fact]
    public void Pagamentos310PreservamMultiplosGruposECartao()
    {
        using var certificate = Fixtures.Certificate();
        var xml = XmlFixture.Parse(Fixtures.Nota(55, "3.10").GetXml());
        var first = xml.Descendants(XmlFixture.Ns + "pag").Single();
        first.Element(XmlFixture.Ns + "vPag")!.Value = "5.00";
        first.AddAfterSelf(new XElement(XmlFixture.Ns + "pag",
            new XElement(XmlFixture.Ns + "tPag", "03"), new XElement(XmlFixture.Ns + "vPag", "5.00"),
            new XElement(XmlFixture.Ns + "card", new XElement(XmlFixture.Ns + "tpIntegra", "2"),
                new XElement(XmlFixture.Ns + "CNPJ", "12345678000195"), new XElement(XmlFixture.Ns + "tBand", "01"), new XElement(XmlFixture.Ns + "cAut", "123456"))));
        var typed = NotaFiscal.Load(xml.ToString());
        Assert.Equal(2, typed.InfNFe.Pag.Count);
        Assert.Equal("03", typed.InfNFe.Pag[1].TPag);
        Assert.Equal(5m, typed.InfNFe.Pag[1].VPag);
        Assert.Equal("123456", typed.InfNFe.Pag[1].Card.CAut);
        Assert.Same(typed.InfNFe, typed.InfNFe.Pag[1].Card.Parent);
        typed.InfNFe.Pag[1].Card.CNPJReceb = "12345678000195";
        var generated = XmlFixture.Parse(typed.GetXml());
        Assert.Empty(generated.Descendants(XmlFixture.Ns + "detPag"));
        Assert.Empty(generated.Descendants(XmlFixture.Ns + "CNPJReceb"));
        var document = typed; document.Assinar(certificate); document.Validar(Fixtures.Config(certificate).Arquivos.PathSchemas);
    }

    [Fact]
    public void Pagamento400UsaDetalhesETrocoSemCamposAntigos()
    {
        using var certificate = Fixtures.Certificate();
        var typed = NotaFiscal.Load(Fixtures.Nota().GetXml());
        var payment = Assert.Single(typed.InfNFe.Pag);
        payment.DetPag[0].VPag = 5m;
        var second = payment.DetPag.AddNew();
        second.TPag = "03"; second.VPag = 5m;
        second.Card = new NFePagCard { TpIntegra = "2", CNPJ = "12345678000195", TBand = "01", CAut = "123456", CNPJReceb = "12345678000195" };
        payment.TPag = "99"; payment.VPag = 999m;
        typed.InfNFe.Ide.IndPag = "0";
        Assert.Same(typed.InfNFe, second.Parent);
        Assert.Same(typed.InfNFe, second.Card.Parent);
        var xml = XmlFixture.Parse(typed.GetXml());
        Assert.Equal(2, xml.Descendants(XmlFixture.Ns + "detPag").Count());
        Assert.Empty(xml.Descendants(XmlFixture.Ns + "pag").Single().Elements(XmlFixture.Ns + "tPag"));
        Assert.Empty(xml.Descendants(XmlFixture.Ns + "ide").Single().Elements(XmlFixture.Ns + "indPag"));
        Assert.Single(xml.Descendants(XmlFixture.Ns + "CNPJReceb"));
        var document = typed; document.Assinar(certificate); document.Validar(Fixtures.Config(certificate).Arquivos.PathSchemas);
    }

    [Theory]
    [InlineData("3.10")]
    [InlineData("4.00")]
    public void MedicamentosPreservamCamposEColecaoConformeLeiaute(string version)
    {
        using var certificate = Fixtures.Certificate();
        var typed = NotaFiscal.Load(Fixtures.Nota(55, version).GetXml());
        var medicines = typed.InfNFe.Det[0].Prod.Med;
        medicines.Add(new NFeDetProdMed { NLote = "LOTE1", QLote = 1m, DFab = "2026-01-01", DVal = "2027-01-01", CProdANVISA = "ISENTO", VPMC = 10m });
        if (version == "3.10") medicines.Add(new NFeDetProdMed { NLote = "LOTE2", QLote = 2m, DFab = "2026-01-02", DVal = "2027-01-02", VPMC = 20m });
        var generated = typed.GetXml();
        var reloaded = NotaFiscal.Load(generated);
        Assert.Equal(medicines.Count, reloaded.InfNFe.Det[0].Prod.Med.Count);
        Assert.All(reloaded.InfNFe.Det[0].Prod.Med, medicine => Assert.Same(reloaded.InfNFe, medicine.Parent));
        var xml = XmlFixture.Parse(generated);
        Assert.Equal(version == "3.10" ? 2 : 0, xml.Descendants(XmlFixture.Ns + "nLote").Count());
        Assert.Equal(version == "4.00" ? 1 : 0, xml.Descendants(XmlFixture.Ns + "cProdANVISA").Count());
        var document = typed; document.Assinar(certificate); document.Validar(Fixtures.Config(certificate).Arquivos.PathSchemas);
    }

    [Fact]
    public void Schema400RejeitaMaisDeUmGrupoDeMedicamentos()
    {
        using var certificate = Fixtures.Certificate();
        var typed = NotaFiscal.Load(Fixtures.Nota().GetXml());
        typed.InfNFe.Det[0].Prod.Med.Add(new NFeDetProdMed { CProdANVISA = "ISENTO", VPMC = 10m });
        typed.InfNFe.Det[0].Prod.Med.Add(new NFeDetProdMed { CProdANVISA = "ISENTO", VPMC = 20m });
        var document = typed; document.Assinar(certificate);
        Assert.Throws<OpenDFeValidationException>(() => document.Validar(Fixtures.Config(certificate).Arquivos.PathSchemas));
    }

    [Fact]
    public void AlterarVersaoDaRaizAtualizaCondicoesDosFilhos()
    {
        var typed = NotaFiscal.Load(Fixtures.Nota(55, "3.10").GetXml());
        typed.InfNFe.Det[0].Imposto.IPI = new NFeDetImpostoIPI { ClEnq = "999", CEnq = "999" };
        typed.InfNFe.Det[0].Imposto.IBSCBS = new NFeDetImpostoIBSCBS { CST = "000", CClassTrib = "000001" };
        var before = XmlFixture.Parse(typed.GetXml());
        Assert.Single(before.Descendants(XmlFixture.Ns + "indPag"));
        Assert.Single(before.Descendants(XmlFixture.Ns + "clEnq"));
        Assert.Empty(before.Descendants(XmlFixture.Ns + "IBSCBS"));
        typed.InfNFe.Versao = NFeVersao.v400;
        var after = XmlFixture.Parse(typed.GetXml());
        Assert.Empty(after.Descendants(XmlFixture.Ns + "ide").Single().Elements(XmlFixture.Ns + "indPag"));
        Assert.Empty(after.Descendants(XmlFixture.Ns + "clEnq"));
        Assert.Single(after.Descendants(XmlFixture.Ns + "IBSCBS"));
        Assert.Same(typed.InfNFe, typed.InfNFe.Det[0].Imposto.IBSCBS.Parent);
    }

    [Theory]
    [InlineData("3.10", false)]
    [InlineData("4.00", true)]
    public void GrupoSuplementarSegueVersaoDoDocumento(string version, bool hasUrl)
    {
        var typed = NotaFiscal.Load(Fixtures.Nota(65, version).GetXml());
        typed.InfNFeSupl = new NFeSupl { QrCode = "https://consulta.test/qrcode?p=chave|3|2", UrlChave = "https://consulta.test" };
        var reloaded = NotaFiscal.Load(typed.GetXml());
        Assert.Equal(typed.InfNFeSupl.QrCode, reloaded.InfNFeSupl.QrCode);
        Assert.Equal(hasUrl, XmlFixture.Parse(typed.GetXml()).Descendants(XmlFixture.Ns + "urlChave").Any());
        Assert.Same(reloaded.InfNFe, reloaded.InfNFeSupl.Parent);
        typed.InfNFe = NotaFiscal.Load(Fixtures.Nota().GetXml()).InfNFe;
        Assert.Same(typed.InfNFe, typed.InfNFeSupl.Parent);
    }
}
