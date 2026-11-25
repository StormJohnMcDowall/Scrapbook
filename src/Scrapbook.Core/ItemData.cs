namespace Scrapbook.Core;

/// <summary>One saved snippet as stored on disk.</summary>
public sealed record ItemData(Guid Id, string Description, string Body, bool Masked);
