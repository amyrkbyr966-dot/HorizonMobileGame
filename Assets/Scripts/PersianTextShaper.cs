using System.Text;

/// <summary>
/// Persian/Arabic text shaper for TextMeshPro.
///
/// TMP does NOT shape Arabic script natively: a logical Persian string
/// ("مسابقه") would render with disconnected letters. This shaper maps
/// each letter to its contextual presentation form (isolated / initial /
/// medial / final, Arabic Presentation Forms-A/B) and emits the string
/// in VISUAL order (right-to-left), so TMP renders it correctly with
/// the Vazirmatn font (which contains all the presentation-form glyphs
/// — verified against the font cmap, 2026-11-10).
///
/// Scope: pure Persian labels (the SettingsManager.GetLabel() strings).
/// ASCII letters/digits are passed through unchanged; mixed BiDi text
/// (Persian + embedded numbers) is NOT handled — do not feed it here.
/// Feed the result to TMP_Text with isRightToLeft = false (default).
/// </summary>
public static class PersianTextShaper
{
    // base, isolated, final, initial, medial (0 = no such form).
    // Table generated from the Unicode ArabicShaping data and verified
    // against the Vazirmatn cmap (every non-zero codepoint exists in
    // the font). Right-joining-only letters have no initial/medial form.
    static readonly int[,] Forms = new int[,]
    {
        { 0x0621, 0xFE80, 0x0000, 0x0000, 0x0000 }, // HAMZA
        { 0x0622, 0xFE81, 0xFE82, 0x0000, 0x0000 }, // ALEF WITH MADDA ABOVE
        { 0x0623, 0xFE83, 0xFE84, 0x0000, 0x0000 }, // ALEF WITH HAMZA ABOVE
        { 0x0624, 0xFE85, 0xFE86, 0x0000, 0x0000 }, // WAW WITH HAMZA ABOVE
        { 0x0625, 0xFE87, 0xFE88, 0x0000, 0x0000 }, // ALEF WITH HAMZA BELOW
        { 0x0626, 0xFE89, 0xFE8A, 0xFE8B, 0xFE8C }, // YEH WITH HAMZA ABOVE
        { 0x0627, 0xFE8D, 0xFE8E, 0x0000, 0x0000 }, // ALEF
        { 0x0628, 0xFE8F, 0xFE90, 0xFE91, 0xFE92 }, // BEH
        { 0x0629, 0xFE93, 0xFE94, 0x0000, 0x0000 }, // TEH MARBUTA
        { 0x062A, 0xFE95, 0xFE96, 0xFE97, 0xFE98 }, // TEH
        { 0x062B, 0xFE99, 0xFE9A, 0xFE9B, 0xFE9C }, // THEH
        { 0x062C, 0xFE9D, 0xFE9E, 0xFE9F, 0xFEA0 }, // JEEM
        { 0x062D, 0xFEA1, 0xFEA2, 0xFEA3, 0xFEA4 }, // HAH
        { 0x062E, 0xFEA5, 0xFEA6, 0xFEA7, 0xFEA8 }, // KHAH
        { 0x062F, 0xFEA9, 0xFEAA, 0x0000, 0x0000 }, // DAL
        { 0x0630, 0xFEAB, 0xFEAC, 0x0000, 0x0000 }, // THAL
        { 0x0631, 0xFEAD, 0xFEAE, 0x0000, 0x0000 }, // REH
        { 0x0632, 0xFEAF, 0xFEB0, 0x0000, 0x0000 }, // ZAIN
        { 0x0633, 0xFEB1, 0xFEB2, 0xFEB3, 0xFEB4 }, // SEEN
        { 0x0634, 0xFEB5, 0xFEB6, 0xFEB7, 0xFEB8 }, // SHEEN
        { 0x0635, 0xFEB9, 0xFEBA, 0xFEBB, 0xFEBC }, // SAD
        { 0x0636, 0xFEBD, 0xFEBE, 0xFEBF, 0xFEC0 }, // DAD
        { 0x0637, 0xFEC1, 0xFEC2, 0xFEC3, 0xFEC4 }, // TAH
        { 0x0638, 0xFEC5, 0xFEC6, 0xFEC7, 0xFEC8 }, // ZAH
        { 0x0639, 0xFEC9, 0xFECA, 0xFECB, 0xFECC }, // AIN
        { 0x063A, 0xFECD, 0xFECE, 0xFECF, 0xFED0 }, // GHAIN
        { 0x0641, 0xFED1, 0xFED2, 0xFED3, 0xFED4 }, // FEH
        { 0x0642, 0xFED5, 0xFED6, 0xFED7, 0xFED8 }, // QAF
        { 0x0643, 0xFED9, 0xFEDA, 0xFEDB, 0xFEDC }, // KAF
        { 0x0644, 0xFEDD, 0xFEDE, 0xFEDF, 0xFEE0 }, // LAM
        { 0x0645, 0xFEE1, 0xFEE2, 0xFEE3, 0xFEE4 }, // MEEM
        { 0x0646, 0xFEE5, 0xFEE6, 0xFEE7, 0xFEE8 }, // NOON
        { 0x0647, 0xFEE9, 0xFEEA, 0xFEEB, 0xFEEC }, // HEH
        { 0x0648, 0xFEED, 0xFEEE, 0x0000, 0x0000 }, // WAW
        { 0x0649, 0xFEEF, 0xFEF0, 0x0000, 0x0000 }, // ALEF MAKSURA
        { 0x064A, 0xFEF1, 0xFEF2, 0xFEF3, 0xFEF4 }, // YEH
        { 0x0671, 0xFB50, 0xFB51, 0x0000, 0x0000 }, // ALEF WASLA
        { 0x0679, 0xFB66, 0xFB67, 0xFB68, 0xFB69 }, // TTEH
        { 0x067E, 0xFB56, 0xFB57, 0xFB58, 0xFB59 }, // PEH
        { 0x0686, 0xFB7A, 0xFB7B, 0xFB7C, 0xFB7D }, // TCHEH
        { 0x0688, 0xFB88, 0xFB89, 0x0000, 0x0000 }, // DDAL
        { 0x0691, 0xFB8C, 0xFB8D, 0x0000, 0x0000 }, // RREH
        { 0x0698, 0xFB8A, 0xFB8B, 0x0000, 0x0000 }, // JEH
        { 0x06A4, 0xFB6A, 0xFB6B, 0xFB6C, 0xFB6D }, // VEH
        { 0x06A9, 0xFB8E, 0xFB8F, 0xFB90, 0xFB91 }, // KEHEH
        { 0x06AD, 0xFBD3, 0xFBD4, 0xFBD5, 0xFBD6 }, // NG
        { 0x06AF, 0xFB92, 0xFB93, 0xFB94, 0xFB95 }, // GAF
        { 0x06BA, 0xFB9E, 0xFB9F, 0x0000, 0x0000 }, // NOON GHUNNA
        { 0x06BE, 0xFBAA, 0xFBAB, 0xFBAC, 0xFBAD }, // HEH DOACHASHMEE
        { 0x06C6, 0xFBD9, 0xFBDA, 0x0000, 0x0000 }, // OE
        { 0x06C7, 0xFBD7, 0xFBD8, 0x0000, 0x0000 }, // U
        { 0x06C8, 0xFBDB, 0xFBDC, 0x0000, 0x0000 }, // YU
        { 0x06C9, 0xFBE2, 0xFBE3, 0x0000, 0x0000 }, // KIRGHIZ YU
        { 0x06CB, 0xFBDE, 0xFBDF, 0x0000, 0x0000 }, // VE
        { 0x06CC, 0xFBFC, 0xFBFD, 0xFBFE, 0xFBFF }, // FARSI YEH
        { 0x06D2, 0xFBAE, 0xFBAF, 0x0000, 0x0000 }, // YEH BARREE
        { 0x06D3, 0xFBB0, 0xFBB1, 0x0000, 0x0000 }, // YEH BARREE WITH HAMZA ABOVE
    };

    // Lam-alef ligatures: [alef code, isolated, final]
    static readonly int[,] LamAlef = new int[,]
    {
        { 0x0627, 0xFEFB, 0xFEFC }, // LAM + ALEF
        { 0x0622, 0xFEF5, 0xFEF6 }, // LAM + ALEF WITH MADDA ABOVE
        { 0x0623, 0xFEF7, 0xFEF8 }, // LAM + ALEF WITH HAMZA ABOVE
        { 0x0625, 0xFEF9, 0xFEFA }, // LAM + ALEF WITH HAMZA BELOW
    };

    static int RowOf(int c)
    {
        for (int i = 0; i < Forms.GetLength(0); i++)
            if (Forms[i, 0] == c) return i;
        return -1;
    }

    static bool IsLetter(int c) { return RowOf(c) >= 0; }

    /// <summary>True if the letter can join to the FOLLOWING letter
    /// (has initial/medial forms).</summary>
    static bool JoinsForward(int c)
    {
        int r = RowOf(c);
        return r >= 0 && Forms[r, 3] != 0;
    }

    /// <summary>Shape a logical-order Persian string into visual-order
    /// presentation forms, ready for TMP_Text. Returns the input
    /// unchanged when it contains no Arabic-script letters.</summary>
    public static string Shape(string logical)
    {
        if (string.IsNullOrEmpty(logical)) return logical;
        bool anyArabic = false;
        foreach (char ch in logical)
            if (IsLetter(ch)) { anyArabic = true; break; }
        if (!anyArabic) return logical;

        var shaped = new StringBuilder(logical.Length);
        int n = logical.Length;
        for (int i = 0; i < n; i++)
        {
            int c = logical[i];
            int row = RowOf(c);
            if (row < 0)
            {
                shaped.Append((char)c);
                continue;
            }
            // Lam-alef ligature: LAM followed by an ALEF-family letter.
            if (c == 0x0644 && i + 1 < n)
            {
                int next = logical[i + 1];
                for (int l = 0; l < LamAlef.GetLength(0); l++)
                {
                    if (LamAlef[l, 0] != next) continue;
                    bool ligPrevJoins = i > 0 && JoinsForward(logical[i - 1]) && IsLetter(logical[i - 1]);
                    shaped.Append((char)(ligPrevJoins ? LamAlef[l, 2] : LamAlef[l, 1]));
                    i++; // consume the alef
                    goto NextChar;
                }
            }
            bool prevJoins = i > 0 && JoinsForward(logical[i - 1]) && IsLetter(logical[i - 1]);
            bool nextJoins = i + 1 < n && JoinsForward(c) && IsLetter(logical[i + 1]);
            int form;
            if (prevJoins && nextJoins) form = Forms[row, 4];
            else if (prevJoins) form = Forms[row, 2];
            else if (nextJoins) form = Forms[row, 3];
            else form = Forms[row, 1];
            if (form == 0) form = Forms[row, 1]; // safety: fall back to isolated
            shaped.Append((char)form);
        NextChar: ;
        }
        // Visual order for RTL: reverse the shaped sequence.
        char[] arr = shaped.ToString().ToCharArray();
        System.Array.Reverse(arr);
        return new string(arr);
    }

    /// <summary>True if the string contains any Arabic-script letter.</summary>
    public static bool ContainsArabic(string s)
    {
        if (string.IsNullOrEmpty(s)) return false;
        foreach (char ch in s)
            if (IsLetter(ch)) return true;
        return false;
    }
}
