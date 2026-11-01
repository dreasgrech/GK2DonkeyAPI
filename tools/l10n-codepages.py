"""python -I tools/l10n-codepages.py: writes src/GK2DonkeyAPI/LegacyText.cs: the Windows code pages a translator's spreadsheet saves when it isn't told
"CSV UTF-8" (Excel's plain "CSV" writes the system's ANSI code page), as 128-character tables for bytes 0x80-0xFF,
taken from Python's own codecs (never typed by hand: a wrong table garbles silently). The game's Mono has no I18N
assemblies (GraveyardKeeper2_Data/Managed has none), so Encoding.GetEncoding(1252) isn't there. A byte a code page
leaves undefined maps to the same code point, as Windows does. Also prints a few known characters per page, which the
self-test checks."""
import sys

sys.stdout.reconfigure(encoding="utf-8")
import os
OUT = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), "src", "GK2DonkeyAPI", "LegacyText.cs")


def table(cp):
    chars = []
    for b in range(0x80, 0x100):
        try:
            chars.append(bytes([b]).decode(cp))
        except UnicodeDecodeError:
            chars.append(chr(b))
    assert len(chars) == 128 and all(len(c) == 1 for c in chars)
    return "".join(chars)


def literal(s):
    return "".join(f"\\u{ord(c):04X}" for c in s)


PAGES = [("Cp1252", "cp1252", "Western European: French, German, Spanish, Portuguese and English"),
         ("Cp1250", "cp1250", "Central European: Polish"),
         ("Cp1251", "cp1251", "Cyrillic: Russian"),
         ("Cp1254", "cp1254", "Turkish"),
         ("MacRomanPage", "mac_roman", "Mac Roman: Excel's \"CSV (Macintosh)\" in Western languages; Mac Excel's garbling (\"√©\" for \"é\")"),
         ("Cp850", "cp850", "DOS Western: Excel's \"CSV (MS-DOS)\" in Western languages"),
         ("Cp852", "cp852", "DOS Central European: Excel's \"CSV (MS-DOS)\" in Polish"),
         ("Cp857", "cp857", "DOS Turkish: Excel's \"CSV (MS-DOS)\" in Turkish"),
         ("Cp866", "cp866", "DOS Cyrillic: Excel's \"CSV (MS-DOS)\" in Russian"),
         ("MacLatin2", "mac_latin2", "Mac Central European: Excel's \"CSV (Macintosh)\" in Polish"),
         ("MacCyrillic", "mac_cyrillic", "Mac Cyrillic: Excel's \"CSV (Macintosh)\" in Russian"),
         ("MacTurkish", "mac_turkish", "Mac Turkish: Excel's \"CSV (Macintosh)\" in Turkish"),
         ("Koi8R", "koi8_r", "KOI8-R: Russian on Linux"),
         ("Iso88592", "iso8859_2", "ISO-8859-2: Central European on Linux and LibreOffice"),
         ("Iso88595", "iso8859_5", "ISO-8859-5: Cyrillic on Linux"),
         ("Iso88599", "iso8859_9", "ISO-8859-9: Turkish on Linux and LibreOffice"),
         ("Iso885915", "iso8859_15", "ISO-8859-15: Western European with the euro, on Linux and LibreOffice"),
         ("Latin1", "latin_1", "ISO-8859-1: Western European on Linux, LibreOffice and old tools (its 0x80 to 0x9F are control characters)")]

lines = []
for name, cp, what in PAGES:
    t = table(cp)
    lines.append(f"        // {what} ({cp}, bytes 0x80 to 0xFF)")
    chunks = [t[i:i + 16] for i in range(0, 128, 16)]
    lines.append(f"        private const string {name} =")
    for i, ch in enumerate(chunks):
        lines.append(f"            \"{literal(ch)}\"" + (";" if i == len(chunks) - 1 else " +"))
    lines.append("")

body = "\n".join(lines).rstrip("\n")
cs = f'''using System.Collections.Generic;
using System.Text;

namespace GK2DonkeyAPI
{{
    // The text of a translation file a spreadsheet saved in a Windows code page instead of UTF-8 (Excel's plain "CSV"
    // writes the system's ANSI code page), for the languages whose page is known from the file's language id
    // (docs/localization.md, "Translator mistakes"), and every other page a translator's program may save one in or
    // garble UTF-8 through (Candidates, RepairPages). Written by tools/l10n-codepages.py from Python's own codecs (never
    // typed by hand): the game's Mono has no I18N assemblies, so Encoding.GetEncoding(1252) isn't there. Bytes under
    // 0x80 are ASCII in every page; a byte a page leaves undefined maps to the same code point, as Windows does.
    internal static class LegacyText
    {{
{body}

        // The code page a language's spreadsheet saves in (fr, de, es, pt-br... 1252; pl 1250; ru 1251; tr 1254); null:
        // none we can read (ja, zh_cn, ko, or a community language: their files must be UTF-8).
        internal static string PageFor(string languageId, out string name)
        {{
            string b = (languageId ?? "").ToLowerInvariant();
            int cut = b.IndexOfAny(new[] {{ '-', '_' }});
            if (cut > 0) b = b.Substring(0, cut);
            switch (b)
            {{
                case "en": case "fr": case "de": case "es": case "pt": case "it": case "nl": name = "Windows-1252 (Western European)"; return Cp1252;
                case "pl": case "cs": case "hu": name = "Windows-1250 (Central European)"; return Cp1250;
                case "ru": case "uk": case "be": name = "Windows-1251 (Cyrillic)"; return Cp1251;
                case "tr": name = "Windows-1254 (Turkish)"; return Cp1254;
                default: name = null; return null;
            }}
        }}

        internal static string Decode(byte[] data, int start, string page)
        {{
            var sb = new StringBuilder(data.Length - start);
            for (int i = start; i < data.Length; i++)
            {{
                byte b = data[i];
                sb.Append(b < 0x80 ? (char)b : page[b - 0x80]);
            }}
            return sb.ToString();
        }}

        // The reverse, for mojibake: UTF-8 text a spreadsheet opened as Windows-1252 and saved again ("Ã©" for "é") goes
        // back to its bytes, which are then UTF-8 again. False when a character has no byte in the page. Each page's
        // reverse table is made once (a character's first byte in the page).
        internal static bool Encode(string text, string page, out byte[] bytes)
        {{
            Dictionary<char, byte> back = ReverseOf(page);
            bytes = new byte[text.Length];
            for (int i = 0; i < text.Length; i++)
            {{
                char c = text[i];
                if (c < 0x80) {{ bytes[i] = (byte)c; continue; }}
                if (!back.TryGetValue(c, out byte b)) return false;
                bytes[i] = b;
            }}
            return true;
        }}

        private static readonly Dictionary<string, Dictionary<char, byte>> reverse = new Dictionary<string, Dictionary<char, byte>>();

        private static Dictionary<char, byte> ReverseOf(string page)
        {{
            if (reverse.TryGetValue(page, out Dictionary<char, byte> d)) return d;
            d = new Dictionary<char, byte>(page.Length);
            for (int i = page.Length - 1; i >= 0; i--) d[page[i]] = (byte)(0x80 + i); // the first byte wins, as IndexOf did
            reverse[page] = d;
            return d;
        }}

        internal static string Western => Cp1252;

        internal static string MacRoman => MacRomanPage;

        // The pages a non-UTF-8 file of a language may be in, its Windows page first (TranslationFiles.Paged reads it
        // through each and takes the one plausible reading): Windows (Excel's plain "CSV"), DOS ("CSV (MS-DOS)"), Mac
        // ("CSV (Macintosh)"), ISO (Linux, LibreOffice). Empty: none we read (ja, zh_cn, ko, a community language).
        internal static List<(string Name, string Page)> Candidates(string languageId)
        {{
            var l = new List<(string Name, string Page)>();
            switch (BaseOf(languageId))
            {{
                case "en": case "fr": case "de": case "es": case "pt": case "it": case "nl":
                    l.Add(("Windows-1252", Cp1252)); l.Add(("code page 850 (Excel's CSV (MS-DOS))", Cp850));
                    l.Add(("Mac Roman (Excel's CSV (Macintosh))", MacRomanPage)); l.Add(("ISO-8859-15", Iso885915));
                    break;
                case "pl": case "cs": case "hu":
                    l.Add(("Windows-1250", Cp1250)); l.Add(("code page 852 (Excel's CSV (MS-DOS))", Cp852));
                    l.Add(("Mac Central European (Excel's CSV (Macintosh))", MacLatin2)); l.Add(("ISO-8859-2", Iso88592));
                    break;
                case "ru": case "uk": case "be":
                    l.Add(("Windows-1251", Cp1251)); l.Add(("code page 866 (Excel's CSV (MS-DOS))", Cp866));
                    l.Add(("Mac Cyrillic (Excel's CSV (Macintosh))", MacCyrillic)); l.Add(("KOI8-R", Koi8R)); l.Add(("ISO-8859-5", Iso88595));
                    break;
                case "tr":
                    l.Add(("Windows-1254", Cp1254)); l.Add(("code page 857 (Excel's CSV (MS-DOS))", Cp857));
                    l.Add(("Mac Turkish (Excel's CSV (Macintosh))", MacTurkish)); l.Add(("ISO-8859-9", Iso88599));
                    break;
            }}
            return l;
        }}

        // The pages garbled UTF-8 may have been read through before it was saved again (TranslationFiles.Repaired): the
        // language's own, then the Western ones any language's file may pass through (Windows-1252, ISO-8859-1 and -15,
        // Mac Roman).
        internal static List<string> RepairPages(string languageId)
        {{
            var l = new List<string>();
            foreach (var c in Candidates(languageId)) l.Add(c.Page);
            foreach (string p in new[] {{ Cp1252, Latin1, Iso885915, MacRomanPage }}) if (!l.Contains(p)) l.Add(p);
            return l;
        }}

        private static string BaseOf(string languageId)
        {{
            string b = (languageId ?? "").ToLowerInvariant();
            int cut = b.IndexOfAny(new[] {{ '-', '_' }});
            return cut > 0 ? b.Substring(0, cut) : b;
        }}
    }}
}}
'''
open(OUT, "w", encoding="utf-8", newline="\n").write(cs)
print("written", OUT)
for cp, samples in (("cp1252", [0x80, 0x92, 0x9C, 0xE9]), ("cp1250", [0xB3, 0x9C, 0xEA, 0xB9]), ("cp1251", [0xC0, 0xFF, 0xA8, 0xB8]), ("cp1254", [0xF0, 0xFE, 0xDD, 0xFD])):
    print(cp, " ".join(f"{b:02X}={bytes([b]).decode(cp)}(U+{ord(bytes([b]).decode(cp)):04X})" for b in samples))
