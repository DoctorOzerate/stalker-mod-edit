using XRayForge.Core.Projects;

namespace XRayForge.Tests;

/// <summary>Раскладка слотов экипировки: дефолты по сериям, нормализация и сериализация.</summary>
public class EquipSlotLayoutTests
{
    [Theory]
    [InlineData("soc", 3)]
    [InlineData("cs", 4)]
    [InlineData("cop", 5)]
    [InlineData("unknown", 3)]
    public void DefaultLayout_matches_series_reference(string series, int count)
    {
        var layout = EquipSlotCatalog.DefaultLayout(series);

        Assert.Equal(count, layout.Count);
        Assert.All(layout, slot =>
        {
            Assert.False(string.IsNullOrWhiteSpace(slot.Key));
            Assert.True(slot.Width >= EquipSlotCatalog.MinSlotSize);
            Assert.True(slot.Height >= EquipSlotCatalog.MinSlotSize);
            Assert.True(slot.X >= 0);
            Assert.True(slot.Y >= 0);
        });
        Assert.Equal(layout.Count, layout.Select(slot => slot.Key).Distinct().Count());
    }

    [Fact]
    public void CanvasSize_covers_every_slot_with_padding()
    {
        var layout = EquipSlotCatalog.DefaultLayout("cop");
        var (width, height) = EquipSlotCatalog.CanvasSize(layout);

        Assert.True(width >= layout.Max(slot => slot.X + slot.Width));
        Assert.True(height >= layout.Max(slot => slot.Y + slot.Height));
        Assert.Equal(EquipSlotCatalog.EmptyCanvasWidth,
            EquipSlotCatalog.CanvasSize(Array.Empty<EquipSlotLayout>()).Width);
    }

    [Fact]
    public void Normalize_keeps_explicit_empty_layout_and_clamps_invalid_geometry()
    {
        Assert.Empty(EquipSlotCatalog.Normalize(Array.Empty<EquipSlotLayout>(), "soc"));

        var normalized = EquipSlotCatalog.Normalize(new[]
        {
            new EquipSlotLayout { Key = "weapon", X = -10, Y = -2, Width = 1, Height = 0 },
            new EquipSlotLayout { Key = "", X = 20, Y = 30, Width = 50, Height = 60 },
            new EquipSlotLayout { Key = "weapon", X = 40, Y = 50, Width = 70, Height = 80 }
        }, "soc");

        Assert.Equal(3, normalized.Count);
        Assert.Equal(0, normalized[0].X);
        Assert.Equal(0, normalized[0].Y);
        Assert.Equal(EquipSlotCatalog.MinSlotSize, normalized[0].Width);
        Assert.Equal(EquipSlotCatalog.MinSlotSize, normalized[0].Height);
        Assert.Equal("slot_2", normalized[1].Key);
        Assert.Equal("weapon_2", normalized[2].Key);
    }

    [Fact]
    public void Project_roundtrip_preserves_custom_layout()
    {
        var project = ProjectMigration.Empty();
        project.Series = "cop";
        project.EquipSlotLayout = EquipSlotCatalog.DefaultLayout("cop");
        project.EquipSlotLayout[0].Label = "Пистолет игрока";
        project.EquipSlotLayout[0].Width = 96;

        var back = ProjectSerializer.Deserialize(ProjectSerializer.Serialize(project));

        Assert.NotNull(back.EquipSlotLayout);
        Assert.Equal(project.EquipSlotLayout.Count, back.EquipSlotLayout!.Count);
        Assert.Equal("Пистолет игрока", back.EquipSlotLayout[0].Label);
        Assert.Equal(96, back.EquipSlotLayout[0].Width);

        var without = ProjectMigration.Empty();
        Assert.Null(without.EquipSlotLayout);
    }
}
