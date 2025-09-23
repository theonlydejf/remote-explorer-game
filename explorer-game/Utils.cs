using ExplorerGame.Core;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace ExplorerGame.Base;

public static class GameFactory
{
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
                double brightness = (0.299 * pixel.R + 0.587 * pixel.G + 0.114 * pixel.B) / 255.0;

                if(brightness > .5)
                    result[y, x] = "##";
            }
        }
        return TransposeMap(result);
    }
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