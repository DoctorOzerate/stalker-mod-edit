using System.Buffers.Binary;
using XRayForge.Core.Spawn;
using XRayForge.Core.Text;

namespace XRayForge.Tests;

public class SpawnTests
{
    [Fact]
    public void ParseSpawnHeader_reads_version_count_levels()
    {
        var buf = new byte[12];
        BinaryPrimitives.WriteUInt32LittleEndian(buf, 0u);
        BinaryPrimitives.WriteUInt32LittleEndian(buf.AsSpan(4), 44u);
        BinaryPrimitives.WriteUInt32LittleEndian(buf.AsSpan(8), 8u);

        var h = SpawnAnalyzer.ParseSpawnHeader(buf);
        Assert.NotNull(h);
        Assert.Equal(0u, h!.Version);
        Assert.Equal(44u, h.SpawnCount);
        Assert.Equal(8u, h.LevelCount);
    }

    [Fact]
    public void ParseSpawnHeader_rejects_short_buffer()
    {
        Assert.Null(SpawnAnalyzer.ParseSpawnHeader(new byte[4]));
    }

    [Fact]
    public void ReadCString_reads_null_terminated_string()
    {
        var b = new byte[] { 0x61, 0x63, 0x74, 0x6F, 0x72, 0x00, 0x78, 0x79 };
        var r = SpawnAnalyzer.ReadCString(b, 0);
        Assert.Equal("actor", r.Text);
        Assert.Equal(6, r.Next);
    }

    [Fact]
    public void ExtractSpawnStrings_finds_printable_runs()
    {
        var b = new byte[]
        {
            0, 0, 0x61, 0x63, 0x74, 0x6F, 0x72, 0,
            0x73, 0x74, 0x61, 0x6C, 0x6B, 0x65, 0x72, 0
        };
        var s = SpawnAnalyzer.ExtractSpawnStrings(b, 3);
        Assert.Contains(s, x => x.Text == "actor");
        Assert.Contains(s, x => x.Text == "stalker");
    }

    [Fact]
    public void AnalyzeSpawn_extracts_sections()
    {
        var text = GameEncoding.EncodeCp1251("\0actor\0stalker\0wpn_ak74\0");
        var b = new byte[12 + text.Length];
        BinaryPrimitives.WriteUInt32LittleEndian(b, 0u);
        BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(4), 2u);
        BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(8), 1u);
        text.CopyTo(b, 12);

        var a = SpawnAnalyzer.AnalyzeSpawn(b);
        Assert.NotNull(a.Header);
        Assert.Equal(2u, a.Header!.SpawnCount);
        Assert.Contains("actor", a.Sections);
        Assert.Contains("wpn_ak74", a.Sections);
    }
}