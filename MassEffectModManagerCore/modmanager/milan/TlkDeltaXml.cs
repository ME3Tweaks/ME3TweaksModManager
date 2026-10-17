using System.Globalization;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using LegendaryExplorerCore.TLK;
using LegendaryExplorerCore.TLK.ME2ME3;

namespace MilanDelta;

/// <summary>
/// Stable XML used by both the delta builder and installer. Keep this source identical in both
/// projects: UTF-8 without BOM, LF formatting, no tool version, and original entry order.
/// </summary>
public static class TlkDeltaXml
{
    public const string Format = "tlk-xml-v1";

    public static byte[] Decompress(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes);
        return Serialize(new ME2ME3TalkFile(stream, "delta.tlk"));
    }

    public static byte[] Serialize(ME2ME3TalkFile tlk)
    {
        using var stream = new MemoryStream();
        using (var writer = XmlWriter.Create(stream, new XmlWriterSettings
        {
            Encoding = new UTF8Encoding(false), Indent = true, NewLineChars = "\n",
            NewLineHandling = NewLineHandling.Entitize
        }))
        {
            writer.WriteStartElement("tlkFile");
            foreach (var entry in tlk.StringRefs)
            {
                writer.WriteStartElement("String");
                writer.WriteAttributeString("id", entry.StringID.ToString(CultureInfo.InvariantCulture));
                // Negative IDs encode an offset/sentinel rather than localized text.
                writer.WriteString(entry.StringID < 0
                    ? entry.Flags.ToString(CultureInfo.InvariantCulture) : entry.Data);
                writer.WriteEndElement();
            }
            writer.WriteEndElement();
        }
        return stream.ToArray();
    }

    public static byte[] Compress(byte[] xml)
    {
        using var stream = new MemoryStream(xml);
        var document = XDocument.Load(stream, LoadOptions.PreserveWhitespace);
        if (document.Root?.Name != "tlkFile")
            throw new InvalidDataException("Invalid TLK delta XML root.");
        var entries = document.Root.Elements("String").Select(element =>
            new TLKStringRef(int.Parse(element.Attribute("id")!.Value, CultureInfo.InvariantCulture),
                element.Value)).ToList();
        using var result = HuffmanCompression.SaveToTlkStream(entries);
        return result.ToArray();
    }
}
