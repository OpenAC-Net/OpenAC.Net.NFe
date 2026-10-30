using System;
using System.IO;
using System.Threading;
using PdfSharp.Fonts;

namespace OpenAC.Net.NFe.DANFe.PDFSharp.Common;

internal sealed class DANFeFontResolver : IFontResolver
{
#if NET9_0_OR_GREATER
    private static readonly Lock Gate = new();
#else
    private static readonly object Gate = new();
#endif

    internal static void Initialize()
    {
        lock (Gate)
        {
            // Respeitar o resolver da aplicação hospedeira (inclusive DANFSe).
            GlobalFontSettings.FontResolver ??= new DANFeFontResolver();
        }
    }

    public FontResolverInfo ResolveTypeface(string familyName, bool bold, bool italic)
        => new("LiberationSans-" + (bold ? (italic ? "BoldItalic" : "Bold") : (italic ? "Italic" : "Regular")));

    public byte[] GetFont(string faceName)
    {
        using var stream = typeof(DANFeFontResolver).Assembly.GetManifestResourceStream(
                               "OpenAC.Net.NFe.DANFe.PDFSharp.Resources.Fonts." + faceName + ".ttf")
                           ?? throw new InvalidOperationException("Fonte embutida não encontrada: " + faceName);
        using var output = new MemoryStream();
        stream.CopyTo(output);
        return output.ToArray();
    }
}