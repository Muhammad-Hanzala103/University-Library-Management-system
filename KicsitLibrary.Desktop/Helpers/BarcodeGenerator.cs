using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Media;

namespace KicsitLibrary.Desktop.Helpers
{
    public static class BarcodeGenerator
    {
        private static readonly Dictionary<char, string> Code39Patterns = new()
        {
            { '0', "000110100" }, { '1', "100100001" }, { '2', "001100001" }, { '3', "101100000" },
            { '4', "000110001" }, { '5', "100110000" }, { '6', "001110000" }, { '7', "000100101" },
            { '8', "100100100" }, { '9', "001100100" }, { 'A', "100001001" }, { 'B', "001001001" },
            { 'C', "101001000" }, { 'D', "000011001" }, { 'E', "100011000" }, { 'F', "001011000" },
            { 'G', "000001101" }, { 'H', "100001100" }, { 'I', "001001100" }, { 'J', "000011100" },
            { 'K', "100000011" }, { 'L', "001000011" }, { 'M', "101000010" }, { 'N', "000010011" },
            { 'O', "100010010" }, { 'P', "001010010" }, { 'Q', "000000111" }, { 'R', "100000110" },
            { 'S', "001000110" }, { 'T', "000010110" }, { 'U', "110000001" }, { 'V', "011000001" },
            { 'W', "111000000" }, { 'X', "010010001" }, { 'Y', "110010000" }, { 'Z', "011010000" },
            { '-', "010000101" }, { '.', "110000100" }, { ' ', "011000100" }, { '*', "010010100" }
        };

        public static DrawingImage GenerateCode39(string text)
        {
            if (string.IsNullOrEmpty(text)) text = "INVALID";
            
            // Clean text to only Code39 supported chars
            var cleanText = new StringBuilder();
            cleanText.Append("*"); // Start char
            foreach (var ch in text.ToUpperInvariant())
            {
                if (Code39Patterns.ContainsKey(ch) && ch != '*')
                {
                    cleanText.Append(ch);
                }
                else
                {
                    cleanText.Append("-"); // Fallback char
                }
            }
            cleanText.Append("*"); // Stop char

            var patternString = new StringBuilder();
            for (int i = 0; i < cleanText.Length; i++)
            {
                patternString.Append(Code39Patterns[cleanText[i]]);
                if (i < cleanText.Length - 1)
                {
                    patternString.Append("0"); // Gap between characters
                }
            }

            var barcodePattern = patternString.ToString();

            // Render vector bars
            var geometryGroup = new GeometryGroup();
            double xPosition = 0;
            
            // Code 39 bar widths
            const double narrowWidth = 1.0;
            const double wideWidth = 2.5;
            const double barHeight = 50.0;

            for (int i = 0; i < barcodePattern.Length; i++)
            {
                bool isBlackBar = (i % 2 == 0);
                bool isWide = (barcodePattern[i] == '1');
                double width = isWide ? wideWidth : narrowWidth;

                if (isBlackBar)
                {
                    var rect = new Rect(xPosition, 0, width, barHeight);
                    geometryGroup.Children.Add(new RectangleGeometry(rect));
                }

                xPosition += width;
            }

            // Outer margin
            var outerGeometry = new GeometryGroup();
            outerGeometry.Children.Add(geometryGroup);

            var drawing = new GeometryDrawing(Brushes.Black, null, outerGeometry);
            var drawingGroup = new DrawingGroup();
            drawingGroup.Children.Add(drawing);

            return new DrawingImage(drawingGroup);
        }
    }
}
