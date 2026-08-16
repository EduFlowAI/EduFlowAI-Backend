using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace EduFlowAI.AI.Infrastructure.DocumentVerification;

// Two values naming the same thing rarely arrive identical: scans lose Arabic diacritics and dots,
// forms carry different alef and hamza forms, and OCR drops the odd letter. Identifiers get none of
// this leeway - a national id, a birth date or a year differing by one character is a real difference.
internal static class DocumentFieldComparer
{
    private static readonly HashSet<string> ExactFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "NationalId",
        "DateOfBirth",
        "GraduationYear"
    };

    private static readonly HashSet<string> NameFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "FullNameAr",
        "FullNameEn"
    };

    // A document spells these out where a form abbreviates: "جامعة جنوب الوادي" against "جنوب الوادي".
    private static readonly HashSet<string> InstitutionFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "University",
        "Faculty",
        "Major"
    };

    // Below this a single edit is a large part of the value, so only exact equality counts.
    private const int FuzzyMinimumLength = 8;

    public static bool AreEquivalent(string? extracted, string? expected, string? fieldName)
    {
        if (string.IsNullOrWhiteSpace(extracted) || string.IsNullOrWhiteSpace(expected))
        {
            return false;
        }

        var left = Normalize(extracted);
        var right = Normalize(expected);

        if (left.Length == 0 || right.Length == 0)
        {
            return false;
        }

        if (string.Equals(left, right, StringComparison.Ordinal))
        {
            return true;
        }

        // Spacing is cosmetic even on an identifier: the card prints the number as "٣٠٣ ٠٤ ٠٨ ...".
        // Compound Arabic names are written both ways too: "عبدالرحمن" and "عبد الرحمن".
        var leftTight = Tighten(left);
        var rightTight = Tighten(right);

        if (string.Equals(leftTight, rightTight, StringComparison.Ordinal))
        {
            return true;
        }

        if (fieldName is not null && ExactFields.Contains(fieldName))
        {
            return false;
        }

        if (fieldName is not null && NameFields.Contains(fieldName)
            && IsSameName(left, right, leftTight, rightTight))
        {
            return true;
        }

        if (fieldName is not null && InstitutionFields.Contains(fieldName)
            && SharesEveryWord(left, right))
        {
            return true;
        }

        return Math.Min(left.Length, right.Length) >= FuzzyMinimumLength
            && EditDistanceWithin(left, right, budget: 1);
    }

    // A middle or grandfather name may be missing from one side, and a birth certificate records only
    // the child's first name. Both are anchored on the first name: the father's name is a clean suffix
    // of the child's, so without that anchor a parent would verify as the applicant.
    private static bool IsSameName(string left, string right, string leftTight, string rightTight)
    {
        if (leftTight.StartsWith(rightTight, StringComparison.Ordinal)
            || rightTight.StartsWith(leftTight, StringComparison.Ordinal))
        {
            return true;
        }

        var leftParts = left.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var rightParts = right.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        var (shorter, longer) = leftParts.Length <= rightParts.Length
            ? (leftParts, rightParts)
            : (rightParts, leftParts);

        if (shorter.Length < 2 || shorter[0] != longer[0])
        {
            return false;
        }

        var next = 0;
        foreach (var part in shorter)
        {
            next = Array.IndexOf(longer, part, next);
            if (next < 0)
            {
                return false;
            }

            next++;
        }

        return true;
    }

    private static bool SharesEveryWord(string left, string right)
    {
        var leftParts = left.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var rightParts = right.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        var (shorter, longer) = leftParts.Length <= rightParts.Length
            ? (leftParts, rightParts)
            : (rightParts, leftParts);

        // One shared common word must never carry a match on its own.
        return shorter.Length >= 2 && shorter.All(longer.Contains);
    }

    private static string Tighten(string value) => value.Replace(" ", string.Empty);

    // Strips what the eye ignores: case, punctuation, repeated spaces, Arabic diacritics and tatweel,
    // and the interchangeable letter forms. Arabic-Indic digits become their ASCII equivalents.
    public static string Normalize(string value)
    {
        var builder = new StringBuilder(value.Length);
        var lastWasSpace = true;

        foreach (var raw in value.Trim())
        {
            var c = raw switch
            {
                'آ' or 'أ' or 'إ' or 'ٱ' => 'ا', // alef forms
                'ى' => 'ي',                                     // alef maqsura to yaa
                'ة' => 'ه',                                     // taa marbuta to haa
                'ؤ' => 'و',                                     // waw with hamza
                'ئ' => 'ي',                                     // yaa with hamza
                >= '٠' and <= '٩' => (char)('0' + (raw - '٠')),
                >= '۰' and <= '۹' => (char)('0' + (raw - '۰')),
                _ => raw
            };

            if (c == 'ـ' || (c >= 'ً' && c <= 'ٟ') || c == 'ٰ')
            {
                continue; // diacritics and tatweel
            }

            if (char.IsWhiteSpace(c))
            {
                if (!lastWasSpace)
                {
                    builder.Append(' ');
                    lastWasSpace = true;
                }
                continue;
            }

            if (!char.IsLetterOrDigit(c))
            {
                continue;
            }

            builder.Append(char.ToLowerInvariant(c));
            lastWasSpace = false;
        }

        return builder.ToString().Trim();
    }

    // True when the two differ by at most `budget` insertions, deletions or substitutions.
    private static bool EditDistanceWithin(string left, string right, int budget)
    {
        if (Math.Abs(left.Length - right.Length) > budget)
        {
            return false;
        }

        int i = 0, j = 0, edits = 0;

        while (i < left.Length && j < right.Length)
        {
            if (left[i] == right[j])
            {
                i++;
                j++;
                continue;
            }

            if (++edits > budget)
            {
                return false;
            }

            if (left.Length > right.Length)
            {
                i++;
            }
            else if (right.Length > left.Length)
            {
                j++;
            }
            else
            {
                i++;
                j++;
            }
        }

        return edits + (left.Length - i) + (right.Length - j) <= budget;
    }
}
