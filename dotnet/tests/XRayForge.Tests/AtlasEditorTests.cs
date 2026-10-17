using XRayForge.Core.Imaging;

namespace XRayForge.Tests;

/// <summary>Геометрия и пиксельные операции вкладки «Иконки» (этап 7.3).</summary>
public class AtlasEditorTests
{
    [Fact]
    public void Grid_uses_floor_cells_and_clamps_selection()
    {
        var grid = AtlasGrid.Create(160, 96, 32, 32, 5);

        Assert.Equal(5, grid.Columns);
        Assert.Equal(3, grid.Rows);
        Assert.Equal(new AtlasCell(1, 1), grid.HitTest(33, 33));
        Assert.Null(grid.HitTest(160, 0));
        Assert.Null(grid.HitTest(33, 96));
        Assert.Equal(new AtlasCell(4, 1), grid.ClampCell(new AtlasCell(99, 1)));
        Assert.Equal(new AtlasRegion(1, 1, 3, 2),
            AtlasGrid.Selection(new AtlasCell(3, 2), new AtlasCell(1, 1)));
        Assert.Equal(new AtlasRegion(4, 2, 1, 1),
            grid.ClampSelection(new AtlasRegion(10, 10, 5, 5)));
    }

    [Fact]
    public void Pixel_region_maps_cells_to_pixels()
    {
        var grid = AtlasGrid.Create(160, 96, 32, 24, 5);

        Assert.Equal(new AtlasRegion(64, 24, 64, 48), grid.PixelRegion(new AtlasRegion(2, 1, 2, 2)));
    }

    [Fact]
    public void Crop_copies_requested_rectangle()
    {
        var image = new RgbaImage(3, 2, new byte[]
        {
            1, 1, 1, 255, 2, 2, 2, 255, 3, 3, 3, 255,
            4, 4, 4, 255, 5, 5, 5, 255, 6, 6, 6, 255
        });

        var cropped = AtlasPixels.Crop(image, new AtlasRegion(1, 0, 2, 2));

        Assert.Equal(2, cropped.Width);
        Assert.Equal(2, cropped.Height);
        Assert.Equal(new byte[] { 2, 2, 2, 255, 3, 3, 3, 255, 5, 5, 5, 255, 6, 6, 6, 255 },
            cropped.Pixels);
    }

    [Fact]
    public void Fill_and_alpha_only_touch_selection()
    {
        var image = AtlasPixels.Transparent(4, 2);

        AtlasPixels.Fill(image, new AtlasRegion(1, 0, 2, 2), new RgbaColor(10, 20, 30, 255));
        var filled = (0 * 4 + 1) * 4;
        Assert.Equal(new byte[] { 10, 20, 30, 255 }, image.Pixels[filled..(filled + 4)]);
        Assert.Equal(new byte[] { 0, 0, 0, 0 }, image.Pixels[0..4]);

        AtlasPixels.SetAlpha(image, new AtlasRegion(1, 0, 1, 1), 7);
        Assert.Equal(7, image.Pixels[(0 * 4 + 1) * 4 + 3]);
        Assert.Equal(255, image.Pixels[(1 * 4 + 2) * 4 + 3]);
    }

    [Fact]
    public void Paste_clips_at_image_borders()
    {
        var destination = AtlasPixels.Transparent(2, 2);
        var source = new RgbaImage(2, 2, Enumerable.Repeat((byte)9, 16).ToArray());

        AtlasPixels.Paste(destination, source, 1, 1);

        Assert.Equal(9, destination.Pixels[(1 * 2 + 1) * 4]);
        Assert.Equal(0, destination.Pixels[0]);
        Assert.Equal(0, destination.Pixels[(0 * 2 + 1) * 4]);
    }

    [Fact]
    public void Flip_and_rotate_transform_selection()
    {
        var image = new RgbaImage(2, 2, new byte[]
        {
            1, 0, 0, 255, 2, 0, 0, 255,
            3, 0, 0, 255, 4, 0, 0, 255
        });

        AtlasPixels.Flip(image, new AtlasRegion(0, 0, 2, 2), horizontal: true);
        Assert.Equal(new byte[] { 2, 0, 0, 255, 1, 0, 0, 255, 4, 0, 0, 255, 3, 0, 0, 255 },
            image.Pixels);

        // В отличие от JS-формулы (транспонирование) здесь настоящий поворот на 90°.
        AtlasPixels.Rotate90(image, new AtlasRegion(0, 0, 2, 2), clockwise: true);
        Assert.Equal(new byte[] { 4, 0, 0, 255, 2, 0, 0, 255, 3, 0, 0, 255, 1, 0, 0, 255 },
            image.Pixels);
    }

    [Fact]
    public void Duplicate_detection_groups_identical_cells()
    {
        var image = AtlasPixels.Transparent(8, 4);
        AtlasPixels.Fill(image, new AtlasRegion(0, 0, 4, 4), new RgbaColor(255, 0, 0, 255));
        AtlasPixels.Fill(image, new AtlasRegion(4, 0, 4, 4), new RgbaColor(255, 0, 0, 255));

        var duplicates = AtlasPixels.FindDuplicateCells(image, 4, 4);

        Assert.Equal(new[] { new AtlasCell(0, 0), new AtlasCell(1, 0) }, duplicates);
    }

    [Fact]
    public void Spritesheet_places_sprites_in_a_row_with_gap()
    {
        var source = new RgbaImage(8, 4, new byte[8 * 4 * 4]);
        for (var y = 0; y < 4; y++)
        {
            source.Pixels[(y * 8 + 0) * 4] = 1;
            source.Pixels[(y * 8 + 4) * 4] = 2;
        }

        var sheet = AtlasPixels.BuildSpritesheet(
            source, new[] { new AtlasSprite(0, 0, 1, 1), new AtlasSprite(1, 0, 1, 1) }, 4, 4);

        Assert.Equal(14, sheet.Width);
        Assert.Equal(4, sheet.Height);
        Assert.Equal(1, sheet.Pixels[(0 * 14 + 2) * 4]);
        Assert.Equal(2, sheet.Pixels[(0 * 14 + 8) * 4]);
        Assert.Equal(0, sheet.Pixels[(0 * 14 + 6) * 4]);
    }

    [Theory]
    [InlineData("#f00", 255, 0, 0, 255)]
    [InlineData("00ff00", 0, 255, 0, 255)]
    [InlineData("#10203080", 16, 32, 48, 128)]
    [InlineData("#xyz", 0, 0, 0, 0)]
    public void Hex_color_parser_matches_css_forms(
        string value, byte r, byte g, byte b, byte a)
    {
        var ok = RgbaColor.TryParseHex(value, out var color);

        Assert.Equal(r != 0 || g != 0 || b != 0 || a != 0, ok);
        if (ok) Assert.Equal(new RgbaColor(r, g, b, a), color);
    }
}
