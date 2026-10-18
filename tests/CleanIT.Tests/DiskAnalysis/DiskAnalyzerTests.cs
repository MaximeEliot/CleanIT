using System.IO.Abstractions.TestingHelpers;
using CleanIT.Core.DiskAnalysis;
using Microsoft.Extensions.Logging.Abstractions;

namespace CleanIT.Tests.DiskAnalysis;

public class DiskAnalyzerTests
{
    private const string Root = @"D:\Data";
    private readonly MockFileSystem _fs = new();

    private DiskAnalyzer Analyzer => new(_fs, NullLogger<DiskAnalyzer>.Instance);

    private void AddFile(string relative, int size) => _fs.AddFile($@"{Root}\{relative}", new MockFileData(new byte[size]));

    [Fact]
    public async Task Analyze_CumulatesSizesAndSortsChildrenBySize()
    {
        AddFile(@"small\a.bin", 10);
        AddFile(@"big\b.bin", 500);
        AddFile(@"big\nested\c.bin", 300);
        AddFile(@"medium\d.bin", 100);
        AddFile("root.bin", 1);

        var result = await Analyzer.AnalyzeAsync(Root);

        Assert.Equal(911, result.Root.SizeBytes);
        Assert.Equal(5, result.Root.FileCount);
        Assert.Equal(5, result.TotalFiles);
        Assert.Equal(new[] { "big", "medium", "small" }, result.Root.Children.Select(c => c.Name));
        var big = result.Root.Children[0];
        Assert.Equal(800, big.SizeBytes);
        Assert.Equal(300, Assert.Single(big.Children).SizeBytes);
    }

    [Fact]
    public async Task Analyze_KeepsTheLargestFilesInDescendingOrder()
    {
        for (var i = 1; i <= 80; i++)
        {
            AddFile($@"dir{i % 7}\f{i}.bin", i * 10);
        }

        var result = await Analyzer.AnalyzeAsync(Root);

        Assert.Equal(DiskAnalyzer.DefaultLargestFileCount, result.LargestFiles.Count);
        Assert.Equal(800, result.LargestFiles[0].SizeBytes);
        Assert.Equal(310, result.LargestFiles[^1].SizeBytes);
        Assert.True(result.LargestFiles.Zip(result.LargestFiles.Skip(1)).All(p => p.First.SizeBytes >= p.Second.SizeBytes));
    }

    [Fact]
    public async Task Analyze_DoesNotFollowJunctions()
    {
        AddFile(@"real\a.bin", 100);
        AddFile(@"link\a.bin", 100);
        _fs.DirectoryInfo.New($@"{Root}\link").Attributes |= FileAttributes.ReparsePoint;

        var result = await Analyzer.AnalyzeAsync(Root);

        Assert.Equal(100, result.Root.SizeBytes);
        var link = result.Root.Children.Single(c => c.Name == "link");
        Assert.True(link.IsReparsePoint);
        Assert.Equal(0, link.SizeBytes);
    }

    [Fact]
    public async Task Analyze_NeverModifiesTheFileSystem()
    {
        AddFile(@"a\b.bin", 10);
        var before = _fs.AllPaths.ToList();

        await Analyzer.AnalyzeAsync(Root);

        Assert.Equal(before, _fs.AllPaths.ToList());
    }

    [Fact]
    public async Task Analyze_MissingRoot_Throws()
    {
        await Assert.ThrowsAsync<DirectoryNotFoundException>(() => Analyzer.AnalyzeAsync(@"D:\Missing"));
    }

    [Fact]
    public async Task Analyze_Cancelled_Throws()
    {
        AddFile(@"a\b.bin", 10);
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Analyzer.AnalyzeAsync(Root, cancellationToken: cts.Token));
    }
}
