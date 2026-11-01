using System.Collections.Generic;
using System.Text;

namespace GK2DonkeyAPI
{
    // The text of a translation file a spreadsheet saved in a Windows code page instead of UTF-8 (Excel's plain "CSV"
    // writes the system's ANSI code page), for the languages whose page is known from the file's language id
    // (docs/localization.md, "Translator mistakes"), and every other page a translator's program may save one in or
    // garble UTF-8 through (Candidates, RepairPages). Written by tools/l10n-codepages.py from Python's own codecs (never
    // typed by hand): the game's Mono has no I18N assemblies, so Encoding.GetEncoding(1252) isn't there. Bytes under
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

        // Mac Roman: Excel's "CSV (Macintosh)" in Western languages; Mac Excel's garbling ("√©" for "é") (mac_roman, bytes 0x80 to 0xFF)
        private const string MacRomanPage =
            "\u00C4\u00C5\u00C7\u00C9\u00D1\u00D6\u00DC\u00E1\u00E0\u00E2\u00E4\u00E3\u00E5\u00E7\u00E9\u00E8" +
            "\u00EA\u00EB\u00ED\u00EC\u00EE\u00EF\u00F1\u00F3\u00F2\u00F4\u00F6\u00F5\u00FA\u00F9\u00FB\u00FC" +
            "\u2020\u00B0\u00A2\u00A3\u00A7\u2022\u00B6\u00DF\u00AE\u00A9\u2122\u00B4\u00A8\u2260\u00C6\u00D8" +
            "\u221E\u00B1\u2264\u2265\u00A5\u00B5\u2202\u2211\u220F\u03C0\u222B\u00AA\u00BA\u03A9\u00E6\u00F8" +
            "\u00BF\u00A1\u00AC\u221A\u0192\u2248\u2206\u00AB\u00BB\u2026\u00A0\u00C0\u00C3\u00D5\u0152\u0153" +
            "\u2013\u2014\u201C\u201D\u2018\u2019\u00F7\u25CA\u00FF\u0178\u2044\u20AC\u2039\u203A\uFB01\uFB02" +
            "\u2021\u00B7\u201A\u201E\u2030\u00C2\u00CA\u00C1\u00CB\u00C8\u00CD\u00CE\u00CF\u00CC\u00D3\u00D4" +
            "\uF8FF\u00D2\u00DA\u00DB\u00D9\u0131\u02C6\u02DC\u00AF\u02D8\u02D9\u02DA\u00B8\u02DD\u02DB\u02C7";

        // DOS Western: Excel's "CSV (MS-DOS)" in Western languages (cp850, bytes 0x80 to 0xFF)
        private const string Cp850 =
            "\u00C7\u00FC\u00E9\u00E2\u00E4\u00E0\u00E5\u00E7\u00EA\u00EB\u00E8\u00EF\u00EE\u00EC\u00C4\u00C5" +
            "\u00C9\u00E6\u00C6\u00F4\u00F6\u00F2\u00FB\u00F9\u00FF\u00D6\u00DC\u00F8\u00A3\u00D8\u00D7\u0192" +
            "\u00E1\u00ED\u00F3\u00FA\u00F1\u00D1\u00AA\u00BA\u00BF\u00AE\u00AC\u00BD\u00BC\u00A1\u00AB\u00BB" +
            "\u2591\u2592\u2593\u2502\u2524\u00C1\u00C2\u00C0\u00A9\u2563\u2551\u2557\u255D\u00A2\u00A5\u2510" +
            "\u2514\u2534\u252C\u251C\u2500\u253C\u00E3\u00C3\u255A\u2554\u2569\u2566\u2560\u2550\u256C\u00A4" +
            "\u00F0\u00D0\u00CA\u00CB\u00C8\u0131\u00CD\u00CE\u00CF\u2518\u250C\u2588\u2584\u00A6\u00CC\u2580" +
            "\u00D3\u00DF\u00D4\u00D2\u00F5\u00D5\u00B5\u00FE\u00DE\u00DA\u00DB\u00D9\u00FD\u00DD\u00AF\u00B4" +
            "\u00AD\u00B1\u2017\u00BE\u00B6\u00A7\u00F7\u00B8\u00B0\u00A8\u00B7\u00B9\u00B3\u00B2\u25A0\u00A0";

        // DOS Central European: Excel's "CSV (MS-DOS)" in Polish (cp852, bytes 0x80 to 0xFF)
        private const string Cp852 =
            "\u00C7\u00FC\u00E9\u00E2\u00E4\u016F\u0107\u00E7\u0142\u00EB\u0150\u0151\u00EE\u0179\u00C4\u0106" +
            "\u00C9\u0139\u013A\u00F4\u00F6\u013D\u013E\u015A\u015B\u00D6\u00DC\u0164\u0165\u0141\u00D7\u010D" +
            "\u00E1\u00ED\u00F3\u00FA\u0104\u0105\u017D\u017E\u0118\u0119\u00AC\u017A\u010C\u015F\u00AB\u00BB" +
            "\u2591\u2592\u2593\u2502\u2524\u00C1\u00C2\u011A\u015E\u2563\u2551\u2557\u255D\u017B\u017C\u2510" +
            "\u2514\u2534\u252C\u251C\u2500\u253C\u0102\u0103\u255A\u2554\u2569\u2566\u2560\u2550\u256C\u00A4" +
            "\u0111\u0110\u010E\u00CB\u010F\u0147\u00CD\u00CE\u011B\u2518\u250C\u2588\u2584\u0162\u016E\u2580" +
            "\u00D3\u00DF\u00D4\u0143\u0144\u0148\u0160\u0161\u0154\u00DA\u0155\u0170\u00FD\u00DD\u0163\u00B4" +
            "\u00AD\u02DD\u02DB\u02C7\u02D8\u00A7\u00F7\u00B8\u00B0\u00A8\u02D9\u0171\u0158\u0159\u25A0\u00A0";

        // DOS Turkish: Excel's "CSV (MS-DOS)" in Turkish (cp857, bytes 0x80 to 0xFF)
        private const string Cp857 =
            "\u00C7\u00FC\u00E9\u00E2\u00E4\u00E0\u00E5\u00E7\u00EA\u00EB\u00E8\u00EF\u00EE\u0131\u00C4\u00C5" +
            "\u00C9\u00E6\u00C6\u00F4\u00F6\u00F2\u00FB\u00F9\u0130\u00D6\u00DC\u00F8\u00A3\u00D8\u015E\u015F" +
            "\u00E1\u00ED\u00F3\u00FA\u00F1\u00D1\u011E\u011F\u00BF\u00AE\u00AC\u00BD\u00BC\u00A1\u00AB\u00BB" +
            "\u2591\u2592\u2593\u2502\u2524\u00C1\u00C2\u00C0\u00A9\u2563\u2551\u2557\u255D\u00A2\u00A5\u2510" +
            "\u2514\u2534\u252C\u251C\u2500\u253C\u00E3\u00C3\u255A\u2554\u2569\u2566\u2560\u2550\u256C\u00A4" +
            "\u00BA\u00AA\u00CA\u00CB\u00C8\u00D5\u00CD\u00CE\u00CF\u2518\u250C\u2588\u2584\u00A6\u00CC\u2580" +
            "\u00D3\u00DF\u00D4\u00D2\u00F5\u00D5\u00B5\u00E7\u00D7\u00DA\u00DB\u00D9\u00EC\u00FF\u00AF\u00B4" +
            "\u00AD\u00B1\u00F2\u00BE\u00B6\u00A7\u00F7\u00B8\u00B0\u00A8\u00B7\u00B9\u00B3\u00B2\u25A0\u00A0";

        // DOS Cyrillic: Excel's "CSV (MS-DOS)" in Russian (cp866, bytes 0x80 to 0xFF)
        private const string Cp866 =
            "\u0410\u0411\u0412\u0413\u0414\u0415\u0416\u0417\u0418\u0419\u041A\u041B\u041C\u041D\u041E\u041F" +
            "\u0420\u0421\u0422\u0423\u0424\u0425\u0426\u0427\u0428\u0429\u042A\u042B\u042C\u042D\u042E\u042F" +
            "\u0430\u0431\u0432\u0433\u0434\u0435\u0436\u0437\u0438\u0439\u043A\u043B\u043C\u043D\u043E\u043F" +
            "\u2591\u2592\u2593\u2502\u2524\u2561\u2562\u2556\u2555\u2563\u2551\u2557\u255D\u255C\u255B\u2510" +
            "\u2514\u2534\u252C\u251C\u2500\u253C\u255E\u255F\u255A\u2554\u2569\u2566\u2560\u2550\u256C\u2567" +
            "\u2568\u2564\u2565\u2559\u2558\u2552\u2553\u256B\u256A\u2518\u250C\u2588\u2584\u258C\u2590\u2580" +
            "\u0440\u0441\u0442\u0443\u0444\u0445\u0446\u0447\u0448\u0449\u044A\u044B\u044C\u044D\u044E\u044F" +
            "\u0401\u0451\u0404\u0454\u0407\u0457\u040E\u045E\u00B0\u2219\u00B7\u221A\u2116\u00A4\u25A0\u00A0";

        // Mac Central European: Excel's "CSV (Macintosh)" in Polish (mac_latin2, bytes 0x80 to 0xFF)
        private const string MacLatin2 =
            "\u00C4\u0100\u0101\u00C9\u0104\u00D6\u00DC\u00E1\u0105\u010C\u00E4\u010D\u0106\u0107\u00E9\u0179" +
            "\u017A\u010E\u00ED\u010F\u0112\u0113\u0116\u00F3\u0117\u00F4\u00F6\u00F5\u00FA\u011A\u011B\u00FC" +
            "\u2020\u00B0\u0118\u00A3\u00A7\u2022\u00B6\u00DF\u00AE\u00A9\u2122\u0119\u00A8\u2260\u0123\u012E" +
            "\u012F\u012A\u2264\u2265\u012B\u0136\u2202\u2211\u0142\u013B\u013C\u013D\u013E\u0139\u013A\u0145" +
            "\u0146\u0143\u00AC\u221A\u0144\u0147\u2206\u00AB\u00BB\u2026\u00A0\u0148\u0150\u00D5\u0151\u014C" +
            "\u2013\u2014\u201C\u201D\u2018\u2019\u00F7\u25CA\u014D\u0154\u0155\u0158\u2039\u203A\u0159\u0156" +
            "\u0157\u0160\u201A\u201E\u0161\u015A\u015B\u00C1\u0164\u0165\u00CD\u017D\u017E\u016A\u00D3\u00D4" +
            "\u016B\u016E\u00DA\u016F\u0170\u0171\u0172\u0173\u00DD\u00FD\u0137\u017B\u0141\u017C\u0122\u02C7";

        // Mac Cyrillic: Excel's "CSV (Macintosh)" in Russian (mac_cyrillic, bytes 0x80 to 0xFF)
        private const string MacCyrillic =
            "\u0410\u0411\u0412\u0413\u0414\u0415\u0416\u0417\u0418\u0419\u041A\u041B\u041C\u041D\u041E\u041F" +
            "\u0420\u0421\u0422\u0423\u0424\u0425\u0426\u0427\u0428\u0429\u042A\u042B\u042C\u042D\u042E\u042F" +
            "\u2020\u00B0\u0490\u00A3\u00A7\u2022\u00B6\u0406\u00AE\u00A9\u2122\u0402\u0452\u2260\u0403\u0453" +
            "\u221E\u00B1\u2264\u2265\u0456\u00B5\u0491\u0408\u0404\u0454\u0407\u0457\u0409\u0459\u040A\u045A" +
            "\u0458\u0405\u00AC\u221A\u0192\u2248\u2206\u00AB\u00BB\u2026\u00A0\u040B\u045B\u040C\u045C\u0455" +
            "\u2013\u2014\u201C\u201D\u2018\u2019\u00F7\u201E\u040E\u045E\u040F\u045F\u2116\u0401\u0451\u044F" +
            "\u0430\u0431\u0432\u0433\u0434\u0435\u0436\u0437\u0438\u0439\u043A\u043B\u043C\u043D\u043E\u043F" +
            "\u0440\u0441\u0442\u0443\u0444\u0445\u0446\u0447\u0448\u0449\u044A\u044B\u044C\u044D\u044E\u20AC";

        // Mac Turkish: Excel's "CSV (Macintosh)" in Turkish (mac_turkish, bytes 0x80 to 0xFF)
        private const string MacTurkish =
            "\u00C4\u00C5\u00C7\u00C9\u00D1\u00D6\u00DC\u00E1\u00E0\u00E2\u00E4\u00E3\u00E5\u00E7\u00E9\u00E8" +
            "\u00EA\u00EB\u00ED\u00EC\u00EE\u00EF\u00F1\u00F3\u00F2\u00F4\u00F6\u00F5\u00FA\u00F9\u00FB\u00FC" +
            "\u2020\u00B0\u00A2\u00A3\u00A7\u2022\u00B6\u00DF\u00AE\u00A9\u2122\u00B4\u00A8\u2260\u00C6\u00D8" +
            "\u221E\u00B1\u2264\u2265\u00A5\u00B5\u2202\u2211\u220F\u03C0\u222B\u00AA\u00BA\u03A9\u00E6\u00F8" +
            "\u00BF\u00A1\u00AC\u221A\u0192\u2248\u2206\u00AB\u00BB\u2026\u00A0\u00C0\u00C3\u00D5\u0152\u0153" +
            "\u2013\u2014\u201C\u201D\u2018\u2019\u00F7\u25CA\u00FF\u0178\u011E\u011F\u0130\u0131\u015E\u015F" +
            "\u2021\u00B7\u201A\u201E\u2030\u00C2\u00CA\u00C1\u00CB\u00C8\u00CD\u00CE\u00CF\u00CC\u00D3\u00D4" +
            "\uF8FF\u00D2\u00DA\u00DB\u00D9\uF8A0\u02C6\u02DC\u00AF\u02D8\u02D9\u02DA\u00B8\u02DD\u02DB\u02C7";

        // KOI8-R: Russian on Linux (koi8_r, bytes 0x80 to 0xFF)
        private const string Koi8R =
            "\u2500\u2502\u250C\u2510\u2514\u2518\u251C\u2524\u252C\u2534\u253C\u2580\u2584\u2588\u258C\u2590" +
            "\u2591\u2592\u2593\u2320\u25A0\u2219\u221A\u2248\u2264\u2265\u00A0\u2321\u00B0\u00B2\u00B7\u00F7" +
            "\u2550\u2551\u2552\u0451\u2553\u2554\u2555\u2556\u2557\u2558\u2559\u255A\u255B\u255C\u255D\u255E" +
            "\u255F\u2560\u2561\u0401\u2562\u2563\u2564\u2565\u2566\u2567\u2568\u2569\u256A\u256B\u256C\u00A9" +
            "\u044E\u0430\u0431\u0446\u0434\u0435\u0444\u0433\u0445\u0438\u0439\u043A\u043B\u043C\u043D\u043E" +
            "\u043F\u044F\u0440\u0441\u0442\u0443\u0436\u0432\u044C\u044B\u0437\u0448\u044D\u0449\u0447\u044A" +
            "\u042E\u0410\u0411\u0426\u0414\u0415\u0424\u0413\u0425\u0418\u0419\u041A\u041B\u041C\u041D\u041E" +
            "\u041F\u042F\u0420\u0421\u0422\u0423\u0416\u0412\u042C\u042B\u0417\u0428\u042D\u0429\u0427\u042A";

        // ISO-8859-2: Central European on Linux and LibreOffice (iso8859_2, bytes 0x80 to 0xFF)
        private const string Iso88592 =
            "\u0080\u0081\u0082\u0083\u0084\u0085\u0086\u0087\u0088\u0089\u008A\u008B\u008C\u008D\u008E\u008F" +
            "\u0090\u0091\u0092\u0093\u0094\u0095\u0096\u0097\u0098\u0099\u009A\u009B\u009C\u009D\u009E\u009F" +
            "\u00A0\u0104\u02D8\u0141\u00A4\u013D\u015A\u00A7\u00A8\u0160\u015E\u0164\u0179\u00AD\u017D\u017B" +
            "\u00B0\u0105\u02DB\u0142\u00B4\u013E\u015B\u02C7\u00B8\u0161\u015F\u0165\u017A\u02DD\u017E\u017C" +
            "\u0154\u00C1\u00C2\u0102\u00C4\u0139\u0106\u00C7\u010C\u00C9\u0118\u00CB\u011A\u00CD\u00CE\u010E" +
            "\u0110\u0143\u0147\u00D3\u00D4\u0150\u00D6\u00D7\u0158\u016E\u00DA\u0170\u00DC\u00DD\u0162\u00DF" +
            "\u0155\u00E1\u00E2\u0103\u00E4\u013A\u0107\u00E7\u010D\u00E9\u0119\u00EB\u011B\u00ED\u00EE\u010F" +
            "\u0111\u0144\u0148\u00F3\u00F4\u0151\u00F6\u00F7\u0159\u016F\u00FA\u0171\u00FC\u00FD\u0163\u02D9";

        // ISO-8859-5: Cyrillic on Linux (iso8859_5, bytes 0x80 to 0xFF)
        private const string Iso88595 =
            "\u0080\u0081\u0082\u0083\u0084\u0085\u0086\u0087\u0088\u0089\u008A\u008B\u008C\u008D\u008E\u008F" +
            "\u0090\u0091\u0092\u0093\u0094\u0095\u0096\u0097\u0098\u0099\u009A\u009B\u009C\u009D\u009E\u009F" +
            "\u00A0\u0401\u0402\u0403\u0404\u0405\u0406\u0407\u0408\u0409\u040A\u040B\u040C\u00AD\u040E\u040F" +
            "\u0410\u0411\u0412\u0413\u0414\u0415\u0416\u0417\u0418\u0419\u041A\u041B\u041C\u041D\u041E\u041F" +
            "\u0420\u0421\u0422\u0423\u0424\u0425\u0426\u0427\u0428\u0429\u042A\u042B\u042C\u042D\u042E\u042F" +
            "\u0430\u0431\u0432\u0433\u0434\u0435\u0436\u0437\u0438\u0439\u043A\u043B\u043C\u043D\u043E\u043F" +
            "\u0440\u0441\u0442\u0443\u0444\u0445\u0446\u0447\u0448\u0449\u044A\u044B\u044C\u044D\u044E\u044F" +
            "\u2116\u0451\u0452\u0453\u0454\u0455\u0456\u0457\u0458\u0459\u045A\u045B\u045C\u00A7\u045E\u045F";

        // ISO-8859-9: Turkish on Linux and LibreOffice (iso8859_9, bytes 0x80 to 0xFF)
        private const string Iso88599 =
            "\u0080\u0081\u0082\u0083\u0084\u0085\u0086\u0087\u0088\u0089\u008A\u008B\u008C\u008D\u008E\u008F" +
            "\u0090\u0091\u0092\u0093\u0094\u0095\u0096\u0097\u0098\u0099\u009A\u009B\u009C\u009D\u009E\u009F" +
            "\u00A0\u00A1\u00A2\u00A3\u00A4\u00A5\u00A6\u00A7\u00A8\u00A9\u00AA\u00AB\u00AC\u00AD\u00AE\u00AF" +
            "\u00B0\u00B1\u00B2\u00B3\u00B4\u00B5\u00B6\u00B7\u00B8\u00B9\u00BA\u00BB\u00BC\u00BD\u00BE\u00BF" +
            "\u00C0\u00C1\u00C2\u00C3\u00C4\u00C5\u00C6\u00C7\u00C8\u00C9\u00CA\u00CB\u00CC\u00CD\u00CE\u00CF" +
            "\u011E\u00D1\u00D2\u00D3\u00D4\u00D5\u00D6\u00D7\u00D8\u00D9\u00DA\u00DB\u00DC\u0130\u015E\u00DF" +
            "\u00E0\u00E1\u00E2\u00E3\u00E4\u00E5\u00E6\u00E7\u00E8\u00E9\u00EA\u00EB\u00EC\u00ED\u00EE\u00EF" +
            "\u011F\u00F1\u00F2\u00F3\u00F4\u00F5\u00F6\u00F7\u00F8\u00F9\u00FA\u00FB\u00FC\u0131\u015F\u00FF";

        // ISO-8859-15: Western European with the euro, on Linux and LibreOffice (iso8859_15, bytes 0x80 to 0xFF)
        private const string Iso885915 =
            "\u0080\u0081\u0082\u0083\u0084\u0085\u0086\u0087\u0088\u0089\u008A\u008B\u008C\u008D\u008E\u008F" +
            "\u0090\u0091\u0092\u0093\u0094\u0095\u0096\u0097\u0098\u0099\u009A\u009B\u009C\u009D\u009E\u009F" +
            "\u00A0\u00A1\u00A2\u00A3\u20AC\u00A5\u0160\u00A7\u0161\u00A9\u00AA\u00AB\u00AC\u00AD\u00AE\u00AF" +
            "\u00B0\u00B1\u00B2\u00B3\u017D\u00B5\u00B6\u00B7\u017E\u00B9\u00BA\u00BB\u0152\u0153\u0178\u00BF" +
            "\u00C0\u00C1\u00C2\u00C3\u00C4\u00C5\u00C6\u00C7\u00C8\u00C9\u00CA\u00CB\u00CC\u00CD\u00CE\u00CF" +
            "\u00D0\u00D1\u00D2\u00D3\u00D4\u00D5\u00D6\u00D7\u00D8\u00D9\u00DA\u00DB\u00DC\u00DD\u00DE\u00DF" +
            "\u00E0\u00E1\u00E2\u00E3\u00E4\u00E5\u00E6\u00E7\u00E8\u00E9\u00EA\u00EB\u00EC\u00ED\u00EE\u00EF" +
            "\u00F0\u00F1\u00F2\u00F3\u00F4\u00F5\u00F6\u00F7\u00F8\u00F9\u00FA\u00FB\u00FC\u00FD\u00FE\u00FF";

        // ISO-8859-1: Western European on Linux, LibreOffice and old tools (its 0x80 to 0x9F are control characters) (latin_1, bytes 0x80 to 0xFF)
        private const string Latin1 =
            "\u0080\u0081\u0082\u0083\u0084\u0085\u0086\u0087\u0088\u0089\u008A\u008B\u008C\u008D\u008E\u008F" +
            "\u0090\u0091\u0092\u0093\u0094\u0095\u0096\u0097\u0098\u0099\u009A\u009B\u009C\u009D\u009E\u009F" +
            "\u00A0\u00A1\u00A2\u00A3\u00A4\u00A5\u00A6\u00A7\u00A8\u00A9\u00AA\u00AB\u00AC\u00AD\u00AE\u00AF" +
            "\u00B0\u00B1\u00B2\u00B3\u00B4\u00B5\u00B6\u00B7\u00B8\u00B9\u00BA\u00BB\u00BC\u00BD\u00BE\u00BF" +
            "\u00C0\u00C1\u00C2\u00C3\u00C4\u00C5\u00C6\u00C7\u00C8\u00C9\u00CA\u00CB\u00CC\u00CD\u00CE\u00CF" +
            "\u00D0\u00D1\u00D2\u00D3\u00D4\u00D5\u00D6\u00D7\u00D8\u00D9\u00DA\u00DB\u00DC\u00DD\u00DE\u00DF" +
            "\u00E0\u00E1\u00E2\u00E3\u00E4\u00E5\u00E6\u00E7\u00E8\u00E9\u00EA\u00EB\u00EC\u00ED\u00EE\u00EF" +
            "\u00F0\u00F1\u00F2\u00F3\u00F4\u00F5\u00F6\u00F7\u00F8\u00F9\u00FA\u00FB\u00FC\u00FD\u00FE\u00FF";

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
        // back to its bytes, which are then UTF-8 again. False when a character has no byte in the page. Each page's
        // reverse table is made once (a character's first byte in the page).
        internal static bool Encode(string text, string page, out byte[] bytes)
        {
            Dictionary<char, byte> back = ReverseOf(page);
            bytes = new byte[text.Length];
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (c < 0x80) { bytes[i] = (byte)c; continue; }
                if (!back.TryGetValue(c, out byte b)) return false;
                bytes[i] = b;
            }
            return true;
        }

        private static readonly Dictionary<string, Dictionary<char, byte>> reverse = new Dictionary<string, Dictionary<char, byte>>();

        private static Dictionary<char, byte> ReverseOf(string page)
        {
            if (reverse.TryGetValue(page, out Dictionary<char, byte> d)) return d;
            d = new Dictionary<char, byte>(page.Length);
            for (int i = page.Length - 1; i >= 0; i--) d[page[i]] = (byte)(0x80 + i); // the first byte wins, as IndexOf did
            reverse[page] = d;
            return d;
        }

        internal static string Western => Cp1252;

        internal static string MacRoman => MacRomanPage;

        // The pages a non-UTF-8 file of a language may be in, its Windows page first (TranslationFiles.Paged reads it
        // through each and takes the one plausible reading): Windows (Excel's plain "CSV"), DOS ("CSV (MS-DOS)"), Mac
        // ("CSV (Macintosh)"), ISO (Linux, LibreOffice). Empty: none we read (ja, zh_cn, ko, a community language).
        internal static List<(string Name, string Page)> Candidates(string languageId)
        {
            var l = new List<(string Name, string Page)>();
            switch (BaseOf(languageId))
            {
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
            }
            return l;
        }

        // The pages garbled UTF-8 may have been read through before it was saved again (TranslationFiles.Repaired): the
        // language's own, then the Western ones any language's file may pass through (Windows-1252, ISO-8859-1 and -15,
        // Mac Roman).
        internal static List<string> RepairPages(string languageId)
        {
            var l = new List<string>();
            foreach (var c in Candidates(languageId)) l.Add(c.Page);
            foreach (string p in new[] { Cp1252, Latin1, Iso885915, MacRomanPage }) if (!l.Contains(p)) l.Add(p);
            return l;
        }

        private static string BaseOf(string languageId)
        {
            string b = (languageId ?? "").ToLowerInvariant();
            int cut = b.IndexOfAny(new[] { '-', '_' });
            return cut > 0 ? b.Substring(0, cut) : b;
        }
    }
}
