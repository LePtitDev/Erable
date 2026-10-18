using System.Text.Json.Nodes;
using Erable.Testing;
using Erable.Testing.Reference;

namespace Erable.Cli.Tests;

/// <summary>A copy of the reference project and a way to run <c>erable-cli</c> in process.</summary>
public sealed class CliProject : IDisposable
{
    private readonly TempDirectory _dir = TempDirectory.Create();

    public CliProject()
    {
        FolderFingerprint.Copy(Chroniques.CommittedDirectory, Folder);
    }

    public string Folder => _dir.Combine("project");

    /// <summary>A folder next to the project, for outputs.</summary>
    public string Scratch(string name) => _dir.Combine(name);

    public static async Task<CliRun> RunAsync(params string[] args)
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        using var binary = new MemoryStream();
        var code = await CliApp.RunAsync(args, output, error, () => new NonClosingStream(binary));
        return new CliRun(code, output.ToString(), error.ToString(), binary.ToArray());
    }

    public void Dispose() => _dir.Dispose();

    /// <summary>The standard output stays open after a command wrote to it.</summary>
    private sealed class NonClosingStream(Stream inner) : Stream
    {
        public override bool CanRead => false;

        public override bool CanSeek => false;

        public override bool CanWrite => true;

        public override long Length => inner.Length;

        public override long Position { get => inner.Position; set => throw new NotSupportedException(); }

        public override void Flush() => inner.Flush();

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => inner.Write(buffer, offset, count);
    }
}

public sealed record CliRun(int Code, string Output, string Error, byte[] Binary)
{
    public JsonNode Json => JsonNode.Parse(Output) ?? throw new InvalidOperationException("No JSON on the output.");

    public JsonNode ErrorJson => JsonNode.Parse(Error) ?? throw new InvalidOperationException("No JSON on the error output.");
}
