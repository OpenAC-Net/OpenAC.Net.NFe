using OpenAC.Net.DFe.Core.Attributes;

namespace OpenAC.Net.NFe.Common.Types;

public enum CSTIcms
{
    [DFeEnum("00")]
    Cst00 = 0,
    [DFeEnum("10")]
    CstPart10 = 10,
    [DFeEnum("20")]
    Cst20 = 20,
    [DFeEnum("30")]
    Cst30 = 30,
    [DFeEnum("40")]
    Cst40 = 40,
    [DFeEnum("41")]
    CstRep41 = 41,
    [DFeEnum("50")]
    Cst50 = 50,
    [DFeEnum("51")]
    Cst51 = 51,
    [DFeEnum("60")]
    Cst60 = 60,
    [DFeEnum("70")]
    Cst70 = 70,
    [DFeEnum("90")]
    CstPart90 = 90,
}