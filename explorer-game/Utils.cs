using ExplorerGame.Core;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace ExplorerGame.Base;

/// <summary>
/// Factory class for creating game-related data structures,
/// such as maps derived from external resources (e.g. images).
/// </summary>
public static class GameFactory
{
    /// <summary>
    /// Creates a game map from an image file.
    /// Each pixel is converted into a tile or empty space based on brightness.
    /// Bright pixels become tiles, dark pixels remain null.
    /// </summary>
    /// <param name="imgPath">Path to the image file.</param>
    /// <returns>A 2D array of tiles representing the map.</returns>
    /// <exception cref="FileNotFoundException">Thrown if the image file does not exist.</exception>
    public static Tile?[,] MapFromImage(string imgPath)
    {
        if (!File.Exists(imgPath))
            throw new FileNotFoundException();

        using var image = Image.Load<Rgba32>(imgPath);
        int width = image.Width;
        int height = image.Height;
        Tile?[,] result = new Tile?[height, width];

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                var pixel = image[x, y];

                // Calculate perceived brightness using standard NTSC coefficients
                double brightness = (0.299 * pixel.R + 0.587 * pixel.G + 0.114 * pixel.B) / 255.0;

                // Bright pixels -> tile ("##")
                if (brightness > .5)
                    result[y, x] = "##";
            }
        }

        return TransposeMap(result);
    }

    /// <summary>
    /// Transposes a map so that indexing order changes from [row, col] to [x, y].
    /// Ensures consistency with other parts of the system that expect X-first indexing.
    /// </summary>
    /// <param name="originalMap">The original map array in [row, col] form.</param>
    /// <returns>A transposed map array in [x, y] form.</returns>
    private static Tile?[,] TransposeMap(Tile?[,] originalMap)
    {
        int rows = originalMap.GetLength(0);
        int cols = originalMap.GetLength(1);

        Tile?[,] transposedMap = new Tile?[cols, rows];

        for (int i = 0; i < rows; i++)
            for (int j = 0; j < cols; j++)
                transposedMap[j, i] = originalMap[i, j];

        return transposedMap;
    }
}
