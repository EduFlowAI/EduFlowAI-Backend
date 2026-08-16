using System;
using System.Globalization;
using System.Linq;

namespace EduFlowAI.AI.Infrastructure.DocumentVerification;

// An Egyptian national number carries the birth date in its first seven digits: century, YY, MM, DD.
// 30304080200955 is someone born on 2003-04-08. Useful because a card shows the birth date, the issue
// date and the expiry date in the same format, and a birth certificate spells the date out in words.
internal static class EgyptianNationalId
{
    private const int Length = 14;

    public static bool TryGetBirthDate(string? nationalId, out DateOnly birthDate)
    {
        birthDate = default;

        if (string.IsNullOrWhiteSpace(nationalId))
        {
            return false;
        }

        var digits = new string(DocumentFieldComparer.Normalize(nationalId).Where(char.IsDigit).ToArray());
        if (digits.Length != Length)
        {
            return false;
        }

        var century = digits[0] switch
        {
            '2' => 1900,
            '3' => 2000,
            _ => 0
        };

        if (century == 0
            || !int.TryParse(digits.AsSpan(1, 2), NumberStyles.None, CultureInfo.InvariantCulture, out var year)
            || !int.TryParse(digits.AsSpan(3, 2), NumberStyles.None, CultureInfo.InvariantCulture, out var month)
            || !int.TryParse(digits.AsSpan(5, 2), NumberStyles.None, CultureInfo.InvariantCulture, out var day))
        {
            return false;
        }

        if (month is < 1 or > 12 || day < 1 || day > DateTime.DaysInMonth(century + year, month))
        {
            return false;
        }

        birthDate = new DateOnly(century + year, month, day);
        return true;
    }
}
