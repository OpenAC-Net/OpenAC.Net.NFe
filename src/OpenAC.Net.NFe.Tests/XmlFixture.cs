using OpenAC.Net.NFe.Common;
using System.Xml.Linq;

namespace OpenAC.Net.NFe.Tests;

internal static class XmlFixture
{
    internal static readonly XNamespace Ns = NFeConst.Namespace;
    internal static readonly XNamespace Ds = "http://www.w3.org/2000/09/xmldsig#";
    internal static XElement Parse(string xml) => XElement.Parse(xml);
    internal static string Value(XElement element, string name) => (string)element?.Element(Ns + name) ?? "";
}
