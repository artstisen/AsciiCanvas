namespace AsciiCanvas.Tests;

public class CanvasTextureTests
{
    [Fact]
    public void ShadeAt_EveryCell_StaysBetweenZeroAndStrength()
    {
        var texture = new CanvasTexture { Strength = 5 };

        for (var y = 0; y < 40; y++)
        {
            for (var x = 0; x < 120; x++)
            {
                Assert.InRange(texture.ShadeAt(x, y, 120), 0, 5);
            }
        }
    }

    [Fact]
    public void ShadeAt_LeftOfTheFade_IsTheBackground()
    {
        var texture = new CanvasTexture { Fade = 50 };

        for (var y = 0; y < 30; y++)
        {
            for (var x = 0; x <= 50; x++)
            {
                Assert.Equal(0, texture.ShadeAt(x, y, 100));
            }
        }
    }

    [Fact]
    public void ShadeAt_NearTheRightEdge_ShowsTheTexture()
    {
        var texture = new CanvasTexture();

        var shades = Enumerable.Range(0, 30).SelectMany(y => Enumerable.Range(90, 10).Select(x => texture.ShadeAt(x, y, 100)));

        Assert.Contains(shades, shade => shade > 0);
    }

    [Fact]
    public void ShadeAt_RightEdge_IsTheSameInAnyWidth()
    {
        var texture = new CanvasTexture { Dither = false };

        for (var y = 0; y < 30; y++)
        {
            Assert.Equal(texture.ShadeAt(79, y, 80), texture.ShadeAt(119, y, 120));
        }
    }

    [Theory]
    [InlineData(-1, 0, 10)]
    [InlineData(10, 0, 10)]
    [InlineData(0, -1, 10)]
    [InlineData(0, 0, 0)]
    public void ShadeAt_OutsideTheCanvas_IsZero(int column, int row, int columns)
    {
        var texture = new CanvasTexture();

        Assert.Equal(0, texture.ShadeAt(column, row, columns));
    }

    [Fact]
    public void ColorOf_ShadeZero_IsTheBackground()
    {
        var texture = new CanvasTexture { Tint = 0xFFB000 };

        Assert.Equal(0x0C0C0Cu, texture.ColorOf(0, 0x0C0C0C));
    }

    [Fact]
    public void ColorOf_WhiteTint_GivesGraysCloseToThePaletteRamp()
    {
        var texture = new CanvasTexture();

        // (gray 7 of the 256-color ramp is 0x4E4E4E)
        Assert.Equal(0x4F4F4Fu, texture.ColorOf(7, 0x0C0C0C));
    }

    [Fact]
    public void ColorOf_Tint_MixesEachChannelTowardTheTint()
    {
        var texture = new CanvasTexture { Tint = 0xFF0000 };

        var color = texture.ColorOf(CanvasTexture.MaxStrength, 0x000000);

        Assert.Equal(0x500000u, color);
    }

    [Theory]
    [InlineData(0, 60)]
    [InlineData(9, 60)]
    [InlineData(7, 9)]
    [InlineData(7, 101)]
    public void Init_ValueOutOfRange_Throws(int strength, int fade)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new CanvasTexture { Strength = strength, Fade = fade });
    }

    [Fact]
    public void Init_TintAboveRgb_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new CanvasTexture { Tint = 0x1000000 });
    }

    [Fact]
    public void Texture_Changed_RaisesPresentedOnlyWhenItDiffers()
    {
        using var canvas = new CellCanvas(10, 2);
        var raised = 0;
        canvas.Presented += () => raised++;

        canvas.Texture = new CanvasTexture();
        canvas.Texture = new CanvasTexture();
        canvas.Texture = new CanvasTexture { Strength = 3 };
        canvas.Texture = null;

        Assert.Equal(3, raised);
        Assert.Null(canvas.Texture);
    }
}
