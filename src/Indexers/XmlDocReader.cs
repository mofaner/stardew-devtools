using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace SdvKb.Indexers
{
    public sealed class XmlDocReader
    {
        private readonly Dictionary<string, string> _docs = new Dictionary<string, string>(StringComparer.Ordinal);

        public int Count => _docs.Count;

        public void LoadXmlFile(string xmlPath)
        {
            if (!File.Exists(xmlPath))
                return;

            XDocument doc;
            try
            {
                doc = XDocument.Load(xmlPath);
            }
            catch
            {
                return;
            }

            var members = doc.Root?.Element("members")?.Elements("member");
            if (members == null)
                return;

            foreach (var member in members)
            {
                string? name = member.Attribute("name")?.Value;
                if (string.IsNullOrWhiteSpace(name))
                    continue;

                string formatted = FormatMemberXml(member);
                if (!string.IsNullOrWhiteSpace(formatted))
                {
                    _docs[name!] = formatted;
                }
            }
        }

        public string GetSummary(string? docId)
        {
            if (string.IsNullOrEmpty(docId))
                return string.Empty;
            return _docs.TryGetValue(docId!, out var text) ? text : string.Empty;
        }

        private static string FormatMemberXml(XElement member)
        {
            var sb = new StringBuilder();

            var summaryEl = member.Element("summary");
            if (summaryEl != null)
            {
                string s = CleanXmlNodeText(summaryEl);
                if (!string.IsNullOrWhiteSpace(s))
                    sb.AppendLine(s);
            }

            foreach (var paramEl in member.Elements("param"))
            {
                string? pName = paramEl.Attribute("name")?.Value;
                string pDesc = CleanXmlNodeText(paramEl);
                if (!string.IsNullOrWhiteSpace(pName) && !string.IsNullOrWhiteSpace(pDesc))
                {
                    sb.AppendLine($"@param {pName}: {pDesc}");
                }
            }

            var returnsEl = member.Element("returns");
            if (returnsEl != null)
            {
                string ret = CleanXmlNodeText(returnsEl);
                if (!string.IsNullOrWhiteSpace(ret))
                    sb.AppendLine($"@returns: {ret}");
            }

            var remarksEl = member.Element("remarks");
            if (remarksEl != null)
            {
                string rem = CleanXmlNodeText(remarksEl);
                if (!string.IsNullOrWhiteSpace(rem))
                    sb.AppendLine($"@remarks: {rem}");
            }

            return sb.ToString().Trim();
        }

        private static string CleanXmlNodeText(XElement element)
        {
            var sb = new StringBuilder();
            foreach (var node in element.Nodes())
            {
                if (node is XText t)
                {
                    sb.Append(t.Value);
                }
                else if (node is XElement child)
                {
                    string local = child.Name.LocalName;
                    if (local == "see" || local == "seealso")
                    {
                        string? cref = child.Attribute("cref")?.Value ?? child.Attribute("langword")?.Value;
                        if (!string.IsNullOrEmpty(cref))
                        {
                            // Strip prefix like T: or M: and shorten namespace if long
                            if (cref!.Length > 2 && cref[1] == ':')
                                cref = cref.Substring(2);
                            sb.Append('`').Append(cref).Append('`');
                        }
                        else
                        {
                            sb.Append(CleanXmlNodeText(child));
                        }
                    }
                    else if (local == "paramref" || local == "typeparamref")
                    {
                        string? n = child.Attribute("name")?.Value;
                        if (!string.IsNullOrEmpty(n))
                            sb.Append('`').Append(n).Append('`');
                    }
                    else if (local == "c" || local == "code")
                    {
                        sb.Append('`').Append(child.Value.Trim()).Append('`');
                    }
                    else if (local == "para" || local == "br")
                    {
                        sb.Append(' ').Append(CleanXmlNodeText(child)).Append(' ');
                    }
                    else
                    {
                        sb.Append(CleanXmlNodeText(child));
                    }
                }
            }

            return Regex.Replace(sb.ToString(), @"\s+", " ").Trim();
        }
    }
}
