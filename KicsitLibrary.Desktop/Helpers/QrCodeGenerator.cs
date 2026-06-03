using System;
using System.Windows;
using System.Windows.Media;

namespace KicsitLibrary.Desktop.Helpers
{
    public static class QrCodeGenerator
    {
        public static DrawingImage GenerateQRCode(string text)
        {
            if (string.IsNullOrEmpty(text)) text = "KICSIT";

            const int gridSize = 25; // 25x25 grid
            bool[,] matrix = new bool[gridSize, gridSize];

            // 1. Seed LCG based on text hash
            int seed = 17;
            foreach (char c in text)
            {
                seed = seed * 31 + c;
            }
            seed = Math.Abs(seed);

            int NextRandom()
            {
                seed = (seed * 1103515245 + 12345) & 0x7fffffff;
                return seed;
            }

            // 2. Fill matrix with deterministic pseudorandom noise
            for (int r = 0; r < gridSize; r++)
            {
                for (int c = 0; c < gridSize; c++)
                {
                    // Threshold at 50% density
                    matrix[r, c] = (NextRandom() % 100) < 50;
                }
            }

            // Helper to draw finder pattern
            void ApplyFinder(int rowOffset, int colOffset)
            {
                for (int r = 0; r < 7; r++)
                {
                    for (int c = 0; c < 7; c++)
                    {
                        bool isBlack = true;
                        
                        // Inner 5x5 white frame
                        if ((r == 1 || r == 5) && c >= 1 && c <= 5) isBlack = false;
                        if ((c == 1 || c == 5) && r >= 1 && r <= 5) isBlack = false;

                        matrix[rowOffset + r, colOffset + c] = isBlack;
                    }
                }
            }

            // 3. Overlay finder patterns in three corners
            ApplyFinder(0, 0);                         // Top-Left
            ApplyFinder(0, gridSize - 7);              // Top-Right
            ApplyFinder(gridSize - 7, 0);              // Bottom-Left

            // 4. Overlay alignment pattern (small 5x5 at bottom-right area, e.g. row 16-20, col 16-20)
            int alignR = gridSize - 9;
            int alignC = gridSize - 9;
            for (int r = 0; r < 5; r++)
            {
                for (int c = 0; c < 5; c++)
                {
                    bool isBlack = true;
                    if ((r == 1 || r == 3) && c >= 1 && c <= 3) isBlack = false;
                    if ((c == 1 || c == 3) && r >= 1 && r <= 3) isBlack = false;
                    matrix[alignR + r, alignC + c] = isBlack;
                }
            }

            // 5. Draw vector geometry
            var geometryGroup = new GeometryGroup();
            const double cellSize = 4.0;

            for (int r = 0; r < gridSize; r++)
            {
                for (int c = 0; c < gridSize; c++)
                {
                    if (matrix[r, c])
                    {
                        var rect = new Rect(c * cellSize, r * cellSize, cellSize, cellSize);
                        geometryGroup.Children.Add(new RectangleGeometry(rect));
                    }
                }
            }

            var drawing = new GeometryDrawing(Brushes.Black, null, geometryGroup);
            var drawingGroup = new DrawingGroup();
            drawingGroup.Children.Add(drawing);

            return new DrawingImage(drawingGroup);
        }
    }
}
