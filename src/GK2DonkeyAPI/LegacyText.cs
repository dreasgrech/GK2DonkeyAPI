using System.Text;

namespace GK2DonkeyAPI
{
    // The text of a translation file a spreadsheet saved in a Windows code page instead of UTF-8 (Excel's plain "CSV"
    // writes the system's ANSI code page), for the languages whose page is known from the file's language id
    // (docs/localization.md, "Translator mistakes"). Written by tools/l10n-codepages.py from Python's own codecs (cp1252,
    // cp1250, cp1251, cp1254; never typed by hand): the game's Mono has no I18N assemblies, so Encoding.GetEncoding(1252) isn't there. Bytes under
    // 0x80 are ASCII in every page; a byte a page leaves undefined maps to the same code point, as Windows does.
    internal static class LegacyText
    {
        // Western European: French, German, Spanish, Portuguese and English (cp1252, bytes 0x80 to 0xFF)
        private const string Cp1252 =
            "\u20AC\u0081\u201A\u0192\u201E\u2026\u2020\u2021\u02C6\u2030\u0160\u2039\u0152\u008D\u017D\u008F" +
            "\u0090\u2018\u2019\u201C\u201D\u2022\u2013\u2014\u02DC\u2122\u0161\u203A\u0153\u009D\u017E\u0178" +
            "\u00A0\u00A1\u00A2\u00A3\u00A4\u00A5\u00A6\u00A7\u00A8\u00A9\u00AA\u00AB\u00AC\u00AD\u00AE\u00AF" +
            "\u00B0\u00B1\u00B2\u00B3\u00B4\u00B5\u00B6\u00B7\u00B8\u00B9\u00BA\u00BB\u00BC\u00BD\u00BE\u00BF" +
            "\u00C0\u00C1\u00C2\u00C3\u00C4\u00C5\u00C6\u00C7\u00C8\u00C9\u00CA\u00CB\u00CC\u00CD\u00CE\u00CF" +
            "\u00D0\u00D1\u00D2\u00D3\u00D4\u00D5\u00D6\u00D7\u00D8\u00D9\u00DA\u00DB\u00DC\u00DD\u00DE\u00DF" +
            "\u00E0\u00E1\u00E2\u00E3\u00E4\u00E5\u00E6\u00E7\u00E8\u00E9\u00EA\u00EB\u00EC\u00ED\u00EE\u00EF" +
            "\u00F0\u00F1\u00F2\u00F3\u00F4\u00F5\u00F6\u00F7\u00F8\u00F9\u00FA\u00FB\u00FC\u00FD\u00FE\u00FF";

        // Central European: Polish (cp1250, bytes 0x80 to 0xFF)
        private const string Cp1250 =
            "\u20AC\u0081\u201A\u0083\u201E\u2026\u2020\u2021\u0088\u2030\u0160\u2039\u015A\u0164\u017D\u0179" +
            "\u0090\u2018\u2019\u201C\u201D\u2022\u2013\u2014\u0098\u2122\u0161\u203A\u015B\u0165\u017E\u017A" +
            "\u00A0\u02C7\u02D8\u0141\u00A4\u0104\u00A6\u00A7\u00A8\u00A9\u015E\u00AB\u00AC\u00AD\u00AE\u017B" +
            "\u00B0\u00B1\u02DB\u0142\u00B4\u00B5\u00B6\u00B7\u00B8\u0105\u015F\u00BB\u013D\u02DD\u013E\u017C" +
            "\u0154\u00C1\u00C2\u0102\u00C4\u0139\u0106\u00C7\u010C\u00C9\u0118\u00CB\u011A\u00CD\u00CE\u010E" +
            "\u0110\u0143\u0147\u00D3\u00D4\u0150\u00D6\u00D7\u0158\u016E\u00DA\u0170\u00DC\u00DD\u0162\u00DF" +
            "\u0155\u00E1\u00E2\u0103\u00E4\u013A\u0107\u00E7\u010D\u00E9\u0119\u00EB\u011B\u00ED\u00EE\u010F" +
            "\u0111\u0144\u0148\u00F3\u00F4\u0151\u00F6\u00F7\u0159\u016F\u00FA\u0171\u00FC\u00FD\u0163\u02D9";

        // Cyrillic: Russian (cp1251, bytes 0x80 to 0xFF)
        private const string Cp1251 =
            "\u0402\u0403\u201A\u0453\u201E\u2026\u2020\u2021\u20AC\u2030\u0409\u2039\u040A\u040C\u040B\u040F" +
            "\u0452\u2018\u2019\u201C\u201D\u2022\u2013\u2014\u0098\u2122\u0459\u203A\u045A\u045C\u045B\u045F" +
            "\u00A0\u040E\u045E\u0408\u00A4\u0490\u00A6\u00A7\u0401\u00A9\u0404\u00AB\u00AC\u00AD\u00AE\u0407" +
            "\u00B0\u00B1\u0406\u0456\u0491\u00B5\u00B6\u00B7\u0451\u2116\u0454\u00BB\u0458\u0405\u0455\u0457" +
            "\u0410\u0411\u0412\u0413\u0414\u0415\u0416\u0417\u0418\u0419\u041A\u041B\u041C\u041D\u041E\u041F" +
            "\u0420\u0421\u0422\u0423\u0424\u0425\u0426\u0427\u0428\u0429\u042A\u042B\u042C\u042D\u042E\u042F" +
            "\u0430\u0431\u0432\u0433\u0434\u0435\u0436\u0437\u0438\u0439\u043A\u043B\u043C\u043D\u043E\u043F" +
            "\u0440\u0441\u0442\u0443\u0444\u0445\u0446\u0447\u0448\u0449\u044A\u044B\u044C\u044D\u044E\u044F";

        // Turkish (cp1254, bytes 0x80 to 0xFF)
        private const string Cp1254 =
            "\u20AC\u0081\u201A\u0192\u201E\u2026\u2020\u2021\u02C6\u2030\u0160\u2039\u0152\u008D\u008E\u008F" +
            "\u0090\u2018\u2019\u201C\u201D\u2022\u2013\u2014\u02DC\u2122\u0161\u203A\u0153\u009D\u009E\u0178" +
            "\u00A0\u00A1\u00A2\u00A3\u00A4\u00A5\u00A6\u00A7\u00A8\u00A9\u00AA\u00AB\u00AC\u00AD\u00AE\u00AF" +
            "\u00B0\u00B1\u00B2\u00B3\u00B4\u00B5\u00B6\u00B7\u00B8\u00B9\u00BA\u00BB\u00BC\u00BD\u00BE\u00BF" +
            "\u00C0\u00C1\u00C2\u00C3\u00C4\u00C5\u00C6\u00C7\u00C8\u00C9\u00CA\u00CB\u00CC\u00CD\u00CE\u00CF" +
            "\u011E\u00D1\u00D2\u00D3\u00D4\u00D5\u00D6\u00D7\u00D8\u00D9\u00DA\u00DB\u00DC\u0130\u015E\u00DF" +
            "\u00E0\u00E1\u00E2\u00E3\u00E4\u00E5\u00E6\u00E7\u00E8\u00E9\u00EA\u00EB\u00EC\u00ED\u00EE\u00EF" +
            "\u011F\u00F1\u00F2\u00F3\u00F4\u00F5\u00F6\u00F7\u00F8\u00F9\u00FA\u00FB\u00FC\u0131\u015F\u00FF";

        // The code page a language's spreadsheet saves in (fr, de, es, pt-br... 1252; pl 1250; ru 1251; tr 1254); null:
        // none we can read (ja, zh_cn, ko, or a community language: their files must be UTF-8).
        internal static string PageFor(string languageId, out string name)
        {
            string b = (languageId ?? "").ToLowerInvariant();
            int cut = b.IndexOfAny(new[] { '-', '_' });
            if (cut > 0) b = b.Substring(0, cut);
            switch (b)
            {
                case "en": case "fr": case "de": case "es": case "pt": case "it": case "nl": name = "Windows-1252 (Western European)"; return Cp1252;
                case "pl": case "cs": case "hu": name = "Windows-1250 (Central European)"; return Cp1250;
                case "ru": case "uk": case "be": name = "Windows-1251 (Cyrillic)"; return Cp1251;
                case "tr": name = "Windows-1254 (Turkish)"; return Cp1254;
                default: name = null; return null;
            }
        }

        internal static string Decode(byte[] data, int start, string page)
        {
            var sb = new StringBuilder(data.Length - start);
            for (int i = start; i < data.Length; i++)
            {
                byte b = data[i];
                sb.Append(b < 0x80 ? (char)b : page[b - 0x80]);
            }
            return sb.ToString();
        }

        // The reverse, for mojibake: UTF-8 text a spreadsheet opened as Windows-1252 and saved again ("Ã©" for "é") goes
        // back to its bytes, which are then UTF-8 again. False when a character has no byte in the page.
        internal static bool Encode(string text, string page, out byte[] bytes)
        {
            bytes = new byte[text.Length];
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (c < 0x80) { bytes[i] = (byte)c; continue; }
                int at = page.IndexOf(c);
                if (at < 0) return false;
                bytes[i] = (byte)(0x80 + at);
            }
            return true;
        }

        internal static string Western => Cp1252;
    }
}
