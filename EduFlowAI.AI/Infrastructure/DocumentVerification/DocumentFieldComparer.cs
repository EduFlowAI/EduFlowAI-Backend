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

        if (fieldName is not null && ExactFields.Contains(fieldName))
        {
            return false;
        }

        return Math.Min(left.Length, right.Length) >= FuzzyMinimumLength
            && EditDistanceWithin(left, right, budget: 1);
    }

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
