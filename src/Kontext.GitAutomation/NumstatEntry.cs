using System.Globalization;

namespace Kontext.GitAutomation;

/// <summary>Line counts of one file; both null when git reports the file as binary.</summary>
internal readonly record struct NumstatEntry(int? Additions, int? Deletions);
