using NotaFiscal = OpenAC.Net.NFe.Common.Model.NFe;
using OpenAC.Net.NFe.Common.Types;
using System.Xml.Linq;
using OpenAC.Net.DFe.Core;
using System.Threading.Tasks;

namespace OpenAC.Net.NFe.Tests;

public sealed class UnifiedModelTests
{
    [Test]
    [Arguments(55, "3.10", NFeVersao.v310)]
    [Arguments(65, "3.10", NFeVersao.v310)]
    [Arguments(55, "4.00", NFeVersao.v400)]
    [Arguments(65, "4.00", NFeVersao.v400)]
    public async Task MesmoModeloLeAssinaEValidaOsDoisLeiautes(int model, string version, NFeVersao expected)
    {
        using var certificate = Fixtures.Certificate();
        var source = Fixtures.Nota(model, version);
        var typed = NotaFiscal.Load(source.GetXml());
        await Assert.That(typed.InfNFe.Versao).IsEqualTo(expected);
        await Assert.That(typed.InfNFe.Ide.Parent).IsSameReferenceAs(typed.InfNFe);
        await Assert.That(typed.InfNFe.Det[0].Prod.Parent).IsSameReferenceAs(typed.InfNFe);
        await Assert.That(typed.InfNFe.Det[0].Imposto.ICMS.Parent).IsSameReferenceAs(typed.InfNFe);
        await Assert.That(typed.InfNFe.Id[3..]).IsEqualTo(source.Chave);
        var document = typed;
        document.Assinar(certificate);
        if (model == 65 && version == "4.00") document.GerarQrCode(Fixtures.Config(certificate, model));
        document.Validar(Path.Combine(AppContext.BaseDirectory, "Schemas"));
        await Assert.That(document.ValidarAssinatura()).IsTrue();
        await Assert.That(document.InfNFe.Versao).IsEqualTo(expected);
    }

    [Test]
    public async Task Pagamentos310PreservamMultiplosGruposECartao()
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
        await Assert.That(typed.InfNFe.Pag.Count).IsEqualTo(2);
        await Assert.That(typed.InfNFe.Pag[1].TPag).IsEqualTo(FormaPagamento.fpCartaoCredito);
        await Assert.That(typed.InfNFe.Pag[1].VPag).IsEqualTo(5m);
        await Assert.That(typed.InfNFe.Pag[1].Card.CAut).IsEqualTo("123456");
        await Assert.That(typed.InfNFe.Pag[1].Card.Parent).IsSameReferenceAs(typed.InfNFe);
        typed.InfNFe.Pag[1].Card.CNPJReceb = "12345678000195";
        var generated = XmlFixture.Parse(typed.GetXml());
        await Assert.That(generated.Descendants(XmlFixture.Ns + "detPag")).IsEmpty();
        await Assert.That(generated.Descendants(XmlFixture.Ns + "CNPJReceb")).IsEmpty();
        var document = typed; document.Assinar(certificate); document.Validar(Fixtures.Config(certificate).Arquivos.PathSchemas);
    }

    [Test]
    public async Task Pagamento400UsaDetalhesETrocoSemCamposAntigos()
    {
        using var certificate = Fixtures.Certificate();
        var typed = NotaFiscal.Load(Fixtures.Nota().GetXml());
        var payment = await Assert.That(typed.InfNFe.Pag).HasSingleItem();
        payment.DetPag[0].VPag = 5m;
        var second = payment.DetPag.AddNew();
        second.TPag = FormaPagamento.fpCartaoCredito; second.VPag = 5m;
        second.Card = new NFePagCard { TpIntegra = (TipoIntegracaoPagamento)2, CNPJ = "12345678000195", TBand = BandeiraCartao.bcVisa, CAut = "123456", CNPJReceb = "12345678000195" };
        payment.TPag = FormaPagamento.fpOutro; payment.VPag = 999m;
        typed.InfNFe.Ide.IndPag = IndPagamento.AVista;
        await Assert.That(second.Parent).IsSameReferenceAs(typed.InfNFe);
        await Assert.That(second.Card.Parent).IsSameReferenceAs(typed.InfNFe);
        var xml = XmlFixture.Parse(typed.GetXml());
        await Assert.That(xml.Descendants(XmlFixture.Ns + "detPag").Count()).IsEqualTo(2);
        await Assert.That(xml.Descendants(XmlFixture.Ns + "pag").Single().Elements(XmlFixture.Ns + "tPag")).IsEmpty();
        await Assert.That(xml.Descendants(XmlFixture.Ns + "ide").Single().Elements(XmlFixture.Ns + "indPag")).IsEmpty();
        await Assert.That(xml.Descendants(XmlFixture.Ns + "CNPJReceb")).HasSingleItem();
        var document = typed; document.Assinar(certificate); document.Validar(Fixtures.Config(certificate).Arquivos.PathSchemas);
    }

    [Test]
    [Arguments("3.10")]
    [Arguments("4.00")]
    public async Task MedicamentosPreservamCamposEColecaoConformeLeiaute(string version)
    {
        using var certificate = Fixtures.Certificate();
        var typed = NotaFiscal.Load(Fixtures.Nota(55, version).GetXml());
        var medicines = typed.InfNFe.Det[0].Prod.Med;
        medicines.Add(new NFeDetProdMed { NLote = "LOTE1", QLote = 1m, DFab = new DateTime(2026, 1, 1), DVal = new DateTime(2027, 1, 1), CProdANVISA = "ISENTO", VPMC = 10m });
        if (version == "3.10") medicines.Add(new NFeDetProdMed { NLote = "LOTE2", QLote = 2m, DFab = new DateTime(2026, 1, 2), DVal = new DateTime(2027, 1, 2), VPMC = 20m });
        var generated = typed.GetXml();
        var reloaded = NotaFiscal.Load(generated);
        await Assert.That(reloaded.InfNFe.Det[0].Prod.Med.Count).IsEqualTo(medicines.Count);
        foreach (var medicine in reloaded.InfNFe.Det[0].Prod.Med)
            await Assert.That(medicine.Parent).IsSameReferenceAs(reloaded.InfNFe);
        var xml = XmlFixture.Parse(generated);
        await Assert.That(xml.Descendants(XmlFixture.Ns + "nLote").Count()).IsEqualTo(version == "3.10" ? 2 : 0);
        await Assert.That(xml.Descendants(XmlFixture.Ns + "cProdANVISA").Count()).IsEqualTo(version == "4.00" ? 1 : 0);
        var document = typed; document.Assinar(certificate); document.Validar(Fixtures.Config(certificate).Arquivos.PathSchemas);
    }

    [Test]
    public async Task Schema400RejeitaMaisDeUmGrupoDeMedicamentos()
    {
        using var certificate = Fixtures.Certificate();
        var typed = NotaFiscal.Load(Fixtures.Nota().GetXml());
        typed.InfNFe.Det[0].Prod.Med.Add(new NFeDetProdMed { CProdANVISA = "ISENTO", VPMC = 10m });
        typed.InfNFe.Det[0].Prod.Med.Add(new NFeDetProdMed { CProdANVISA = "ISENTO", VPMC = 20m });
        var document = typed; document.Assinar(certificate);
        await Assert.That(() => document.Validar(Fixtures.Config(certificate).Arquivos.PathSchemas)).ThrowsExactly<OpenDFeValidationException>();
    }

    [Test]
    public async Task AlterarVersaoDaRaizAtualizaCondicoesDosFilhos()
    {
        var typed = NotaFiscal.Load(Fixtures.Nota(55, "3.10").GetXml());
        typed.InfNFe.Det[0].Imposto.IPI = new NFeDetImpostoIPI { ClEnq = "999", CEnq = 999 };
        typed.InfNFe.Det[0].Imposto.IBSCBS = new NFeDetImpostoIBSCBS { CST = "000", CClassTrib = "000001" };
        var before = XmlFixture.Parse(typed.GetXml());
        await Assert.That(before.Descendants(XmlFixture.Ns + "indPag")).HasSingleItem();
        await Assert.That(before.Descendants(XmlFixture.Ns + "clEnq")).HasSingleItem();
        await Assert.That(before.Descendants(XmlFixture.Ns + "IBSCBS")).IsEmpty();
        typed.InfNFe.Versao = NFeVersao.v400;
        var after = XmlFixture.Parse(typed.GetXml());
        await Assert.That(after.Descendants(XmlFixture.Ns + "ide").Single().Elements(XmlFixture.Ns + "indPag")).IsEmpty();
        await Assert.That(after.Descendants(XmlFixture.Ns + "clEnq")).IsEmpty();
        await Assert.That(after.Descendants(XmlFixture.Ns + "IBSCBS")).HasSingleItem();
        await Assert.That(typed.InfNFe.Det[0].Imposto.IBSCBS.Parent).IsSameReferenceAs(typed.InfNFe);
    }

    [Test]
    [Arguments("3.10", false)]
    [Arguments("4.00", true)]
    public async Task GrupoSuplementarSegueVersaoDoDocumento(string version, bool hasUrl)
    {
        var typed = NotaFiscal.Load(Fixtures.Nota(65, version).GetXml());
        typed.InfNFeSupl = new NFeSupl { QrCode = "https://consulta.test/qrcode?p=chave|3|2", UrlChave = "https://consulta.test" };
        var reloaded = NotaFiscal.Load(typed.GetXml());
        await Assert.That(reloaded.InfNFeSupl.QrCode).IsEqualTo(typed.InfNFeSupl.QrCode);
        await Assert.That(XmlFixture.Parse(typed.GetXml()).Descendants(XmlFixture.Ns + "urlChave").Any()).IsEqualTo(hasUrl);
        await Assert.That(reloaded.InfNFeSupl.Parent).IsSameReferenceAs(reloaded.InfNFe);
        typed.InfNFe = NotaFiscal.Load(Fixtures.Nota().GetXml()).InfNFe;
        await Assert.That(typed.InfNFeSupl.Parent).IsSameReferenceAs(typed.InfNFe);
    }

    [Test]
    [Arguments("3.10")]
    [Arguments("4.00")]
    public async Task IPIComISSQNPreservaCamposEOrdemExigidaPeloSchema(string version)
    {
        using var certificate = Fixtures.Certificate();
        var nota = Fixtures.Nota(55, version);
        var imposto = nota.InfNFe.Det[0].Imposto;
        imposto.Imposto = new NFeDetImpostoISSQN
        {
            VBC = 10m, VAliq = 5m, VISSQN = 0.50m, CMunFG = 3550308,
            CListServ = "01.01", IndISS = IndicadorISS.iiExigivel, IndIncentivo = indIncentivo.iiNao
        };
        imposto.IPI = new NFeDetImpostoIPI
        {
            CEnq = 999, TipoIPI = new NFeDetImpostoIPIIPINT { CST = CSTIPI.ipi53 }
        };
        imposto.II = new NFeDetImpostoII();
        var xml = XmlFixture.Parse(nota.GetXml());
        var tags = xml.Descendants(XmlFixture.Ns + "imposto").Single().Elements().Select(e => e.Name.LocalName).ToArray();
        await Assert.That(tags).Contains("IPI");
        await Assert.That(tags).Contains("ISSQN");
        await Assert.That(tags).DoesNotContain("ICMS");
        await Assert.That(tags).DoesNotContain("II");
        await Assert.That(Array.IndexOf(tags, "IPI") < Array.IndexOf(tags, "ISSQN")).IsTrue();
        var reloaded = NotaFiscal.Load(xml.ToString());
        await Assert.That(reloaded.InfNFe.Det[0].Imposto.IPI.IPINT).IsNotNull();
        await Assert.That(reloaded.InfNFe.Det[0].Imposto.Imposto).IsTypeOf<NFeDetImpostoISSQN>();
        reloaded.Assinar(certificate);
        reloaded.Validar(Fixtures.Config(certificate).Arquivos.PathSchemas);
        await Assert.That(reloaded.ValidarAssinatura()).IsTrue();
    }

}