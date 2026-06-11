using System;
using System.Collections.Generic;
using System.Linq;

namespace KicsitLibrary.Core.Helpers
{
    public static class LibraryValidator
    {
        public static readonly IReadOnlyList<string> Programs = new List<string>
        {
            "BSCS",
            "BSSE",
            "BSIT",
            "BSAI",
            "MCS",
            "MSCS"
        };

        public static readonly IReadOnlyList<string> Departments = new List<string>
        {
            "Computer Science",
            "Software Engineering",
            "Information Technology",
            "Artificial Intelligence",
            "Administration",
            "Library Staff"
        };

        public static string FormatCnic(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return string.Empty;
            var digits = new string(raw.Where(char.IsDigit).ToArray());
            if (digits.Length > 13)
            {
                digits = digits.Substring(0, 13);
            }
            if (digits.Length == 13)
            {
                return $"{digits.Substring(0, 5)}-{digits.Substring(5, 7)}-{digits.Substring(12, 1)}";
            }
            else if (digits.Length > 5)
            {
                if (digits.Length > 12)
                {
                    return $"{digits.Substring(0, 5)}-{digits.Substring(5, 7)}-{digits.Substring(12)}";
                }
                return $"{digits.Substring(0, 5)}-{digits.Substring(5)}";
            }
            return digits;
        }

        public static bool IsCnicValid(string? cnic)
        {
            if (string.IsNullOrWhiteSpace(cnic)) return true;
            var match = System.Text.RegularExpressions.Regex.Match(cnic.Trim(), @"^\d{5}-\d{7}-\d{1}$");
            return match.Success;
        }

        public static bool IsRegistrationNumberValid(string? regNum)
        {
            if (string.IsNullOrWhiteSpace(regNum)) return false;
            return regNum.All(char.IsDigit);
        }
    }
}
