using System.ComponentModel;
using OpenAC.Net.DFe.Core.Attributes;

namespace OpenAC.Net.NFe.Common.Types;

public enum NFeVersao
{
    [DFeEnum("3.10"), Description("3.10")] v310 = 310,
    [DFeEnum("4.00"), Description("4.00")] v400 = 400
}