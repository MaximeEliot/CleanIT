using System.IO.Abstractions;
using CleanIT.Core.Abstractions;

namespace CleanIT.Core.Models;

/// <summary>Everything a rule may consult to compute its targets.</summary>
public sealed record RuleContext(IFileSystem FileSystem, IEnvironmentInfo Environment);
