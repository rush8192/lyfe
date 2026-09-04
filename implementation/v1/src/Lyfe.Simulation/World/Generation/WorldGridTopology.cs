namespace Lyfe.Simulation.World.Generation;

public static class WorldGridTopology
{
    public static uint ToTileIndex(uint width, uint height, int x, int y)
    {
        ValidateDimensions(width, height);
        var halfHeight = checked((int)height / 2);
        if (y < -halfHeight || y > halfHeight)
        {
            throw new ArgumentOutOfRangeException(nameof(y));
        }

        var wrappedX = Mod(x, checked((int)width));
        return checked((uint)(((y + halfHeight) * checked((int)width)) + wrappedX));
    }

    public static (int X, int Y) ToCoordinates(uint width, uint height, uint tileIndex)
    {
        ValidateDimensions(width, height);
        var tileCount = checked(width * height);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(tileIndex, tileCount);

        var row = tileIndex / width;
        return (checked((int)(tileIndex % width)), checked((int)row - ((int)height / 2)));
    }

    public static uint? Neighbor(
        uint width,
        uint height,
        uint tileIndex,
        int deltaX,
        int deltaY)
    {
        if (Math.Abs(deltaX) + Math.Abs(deltaY) != 1)
        {
            throw new ArgumentException("A grid neighbor must be one cardinal step away.");
        }

        var (x, y) = ToCoordinates(width, height, tileIndex);
        var targetY = y + deltaY;
        var halfHeight = checked((int)height / 2);
        return targetY < -halfHeight || targetY > halfHeight
            ? null
            : ToTileIndex(width, height, x + deltaX, targetY);
    }

    public static int CylindricalManhattanDistance(
        uint width,
        uint height,
        uint first,
        uint second)
    {
        var (firstX, firstY) = ToCoordinates(width, height, first);
        var (secondX, secondY) = ToCoordinates(width, height, second);
        var directX = Math.Abs(firstX - secondX);
        var wrappedX = checked((int)width) - directX;
        return Math.Min(directX, wrappedX) + Math.Abs(firstY - secondY);
    }

    private static int Mod(int value, int modulus)
    {
        var remainder = value % modulus;
        return remainder < 0 ? remainder + modulus : remainder;
    }

    private static void ValidateDimensions(uint width, uint height)
    {
        if (width == 0 || height == 0 || height % 2 == 0)
        {
            throw new ArgumentException("World topology requires nonzero width and odd height.");
        }
    }
}
