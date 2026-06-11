using Xunit;
using KicsitLibrary.Core.Helpers;

namespace KicsitLibrary.Tests
{
    public class LibraryValidatorTests
    {
        [Theory]
        [InlineData("1234512345671", "12345-1234567-1")]
        [InlineData("12345-1234567-1", "12345-1234567-1")]
        [InlineData("12345a1234567b1", "12345-1234567-1")]
        [InlineData("12345", "12345")]
        [InlineData("123456", "12345-6")]
        [InlineData("12345123456719999", "12345-1234567-1")]
        public void FormatCnic_ShouldFormatCorrectly(string raw, string expected)
        {
            var result = LibraryValidator.FormatCnic(raw);
            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("12345-1234567-1", true)]
        [InlineData("1234512345671", false)]
        [InlineData("12345-1234567-a", false)]
        [InlineData("", true)]
        [InlineData(null, true)]
        public void IsCnicValid_ShouldValidateCorrectly(string? cnic, bool expected)
        {
            var result = LibraryValidator.IsCnicValid(cnic);
            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("12345", true)]
        [InlineData("123a5", false)]
        [InlineData("123-5", false)]
        [InlineData("", false)]
        [InlineData(null, false)]
        public void IsRegistrationNumberValid_ShouldValidateCorrectly(string? regNum, bool expected)
        {
            var result = LibraryValidator.IsRegistrationNumberValid(regNum);
            Assert.Equal(expected, result);
        }

        [Fact]
        public void ProgramsAndDepartments_ShouldHaveValidAllowedLists()
        {
            Assert.Contains("BSCS", LibraryValidator.Programs);
            Assert.Contains("BSSE", LibraryValidator.Programs);
            Assert.Contains("Computer Science", LibraryValidator.Departments);
            Assert.Contains("Software Engineering", LibraryValidator.Departments);
        }
    }
}
