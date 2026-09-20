using System.Net;
using System.Net.Sockets;
using System.Text;

using Extrode.Jaunty.FlatFiles.DuckDB;
using Extrode.Jaunty.FlatFiles.FileSources;
using Extrode.Jaunty.FlatFiles.Interfaces;

using Xunit;

namespace Extrode.Jaunty.FlatFiles.DuckDB.Tests.Import;

/// <summary>
/// coverage-gaps-2026-09-20: <c>DuckDb.EnsureExtensionsLoaded(Async)</c>'s remote-URI branch had no
/// test - installing/loading the <c>httpfs</c> extension from DuckDB's real extension repository,
/// and actually reading a file over the wire, both require network reachability this suite never
/// had locally before. This machine reaches <c>extensions.duckdb.org</c> directly, so a minimal
/// local HTTP server (a raw <see cref="TcpListener"/>, not <see cref="System.Net.HttpListener"/> -
/// see <see cref="MiniHttpServer"/>) serving a CSV exercises the same
/// <c>FlatFile.IsRemoteUri</c>/<c>GetDuckDbExtensionForScheme</c> path a real <c>s3://</c> or
/// <c>https://</c> source would (both map to <c>httpfs</c>), without depending on any third-party
/// remote file staying up.
/// </summary>
public sealed class RemoteHttpImportLiveTests : IDisposable
{
    // DuckDB's httpfs client issues ranged GETs and rejects a 200 response whose Content-Length
    // differs from the requested range - MiniHttpServer below always answers with the full body
    // regardless of any Range header, which only agrees with a ranged request when the whole file
    // fits in httpfs's first read. Keep this small; growing it past that threshold breaks both
    // positive tests below with an unrelated "Content-Length mismatches requested range" error.
    private const string CsvBody = "id,name\n1,widget\n2,gadget\n3,gizmo\n";

    private static readonly Lazy<bool> _canReachExtensionRepo = new(ProbeExtensionRepoReachable);

    private readonly MiniHttpServer _server = new(CsvBody);

    public void Dispose()
    {
        _server.Dispose();
        GC.SuppressFinalize(this);
    }

    private static void SkipIfExtensionRepoUnreachable()
    {
        if (!_canReachExtensionRepo.Value)
            Assert.Skip("extensions.duckdb.org is not reachable from this machine/runner.");
    }

    private static bool ProbeExtensionRepoReachable()
    {
        try
        {
            using var client = new TcpClient();
            Task connectTask = client.ConnectAsync("extensions.duckdb.org", 443);
            return connectTask.Wait(TimeSpan.FromSeconds(5)) && client.Connected;
        }
        catch
        {
            return false;
        }
    }

    public class RemoteCsvItem
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
    }

    [Fact]
    public void FlatFileOpen_GivenAnHttpUrl_InstallsHttpfsAndReadsTheRemoteFile()
    {
        SkipIfExtensionRepoUnreachable();

        using IFlatFile db = FlatFile.Open(_server.BaseUrl + "data.csv");

        List<RemoteCsvItem> rows = db.Query<RemoteCsvItem>("SELECT * FROM data ORDER BY id");

        Assert.Equal(3, rows.Count);
        Assert.Equal("widget", rows[0].Name);
        Assert.Equal("gizmo", rows[2].Name);
    }

    [Fact]
    public async Task RegisterSourceAsync_GivenAnHttpUrl_InstallsHttpfsAndReadsTheRemoteFile()
    {
        SkipIfExtensionRepoUnreachable();

        using var db = new DuckDb();
        var source = new CsvFileSource("remote_async", _server.BaseUrl + "data-async.csv", typeof(RemoteCsvItem));

        await db.RegisterSourceAsync(source);
        List<RemoteCsvItem> rows = await db.QueryAsync<RemoteCsvItem>("SELECT * FROM remote_async ORDER BY id");

        Assert.Equal(3, rows.Count);
        Assert.Equal("widget", rows[0].Name);
        Assert.Equal("gizmo", rows[2].Name);
    }

    [Fact(Timeout = 15_000)]
    public void FlatFileOpen_GivenAnUnreachableHttpUrl_ThrowsRatherThanHanging()
    {
        // Proves DuckDB surfaces a connection-refused read failure as a prompt exception rather
        // than hanging - not the R27 batch 13 "don't leave the extension marked loaded" guard.
        // That guard only fires when INSTALL/LOAD itself throws; here httpfs installs and loads
        // fine (this machine can already reach extensions.duckdb.org, verified by the two tests
        // above), and the failure is RegisterSource's later read against the closed port. The
        // [Fact(Timeout = ...)] is what actually backs "RatherThanHanging" - without it, a real
        // hang would hang the test run instead of failing it.
        var probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        int deadPort = ((IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();

        Assert.ThrowsAny<Exception>(() => FlatFile.Open($"http://127.0.0.1:{deadPort}/missing.csv"));
    }

    /// <summary>
    /// A single-connection-at-a-time raw HTTP/1.1 server over a plain <see cref="TcpListener"/>,
    /// bound explicitly to <see cref="IPAddress.Loopback"/> (127.0.0.1, IPv4 only).
    /// </summary>
    /// <remarks>
    /// <see cref="System.Net.HttpListener"/> was tried first and consistently timed out against
    /// DuckDB's httpfs extension's initial HTTP HEAD request. httpfs (via DuckDB's native HTTP
    /// client) and <c>HttpListener</c>'s <c>"localhost"</c> prefix - the only prefix form that does
    /// not require a Windows URL ACL reservation or admin rights - resolve "localhost" to different
    /// loopback addresses in this environment (IPv4 127.0.0.1 vs IPv6 ::1), so the client's
    /// connection never reached the listener at all. A raw <see cref="TcpListener"/> is not subject
    /// to the URL ACL restriction and can bind an explicit IPv4 loopback address directly, which
    /// sidesteps the mismatch entirely.
    /// </remarks>
    private sealed class MiniHttpServer : IDisposable
    {
        private readonly TcpListener _listener;
        private readonly CancellationTokenSource _cts = new();
        private readonly Task _acceptLoop;
        private readonly byte[] _body;

        public MiniHttpServer(string body)
        {
            _body = Encoding.ASCII.GetBytes(body);
            _listener = new TcpListener(IPAddress.Loopback, 0);
            _listener.Start();
            BaseUrl = $"http://127.0.0.1:{((IPEndPoint)_listener.LocalEndpoint).Port}/";
            _acceptLoop = Task.Run(() => AcceptLoopAsync(_cts.Token));
        }

        public string BaseUrl { get; }

        public void Dispose()
        {
            _cts.Cancel();
            _listener.Stop();
            try { _acceptLoop.Wait(TimeSpan.FromSeconds(2)); } catch { /* best-effort shutdown */ }
            _cts.Dispose();
        }

        private async Task AcceptLoopAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                TcpClient client;
                try
                {
                    client = await _listener.AcceptTcpClientAsync(cancellationToken).ConfigureAwait(false);
                }
                catch (Exception)
                {
                    return;
                }

                _ = Task.Run(() => HandleAsync(client, cancellationToken), cancellationToken);
            }
        }

        private async Task HandleAsync(TcpClient client, CancellationToken cancellationToken)
        {
            using (client)
            using (NetworkStream stream = client.GetStream())
            {
                // Only the request line matters here (method + path); headers are read and
                // discarded up to the blank line that terminates them.
                string requestLine = await ReadLineAsync(stream, cancellationToken).ConfigureAwait(false);
                string method = requestLine.Split(' ', 3)[0];

                string line;
                do
                {
                    line = await ReadLineAsync(stream, cancellationToken).ConfigureAwait(false);
                } while (line.Length > 0);

                string headers =
                    "HTTP/1.1 200 OK\r\n" +
                    "Content-Type: text/csv\r\n" +
                    $"Content-Length: {_body.Length}\r\n" +
                    "Connection: close\r\n" +
                    "Accept-Ranges: none\r\n" +
                    "\r\n";

                byte[] headerBytes = Encoding.ASCII.GetBytes(headers);
                await stream.WriteAsync(headerBytes, cancellationToken).ConfigureAwait(false);

                if (!string.Equals(method, "HEAD", StringComparison.OrdinalIgnoreCase))
                    await stream.WriteAsync(_body, cancellationToken).ConfigureAwait(false);

                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
            }
        }

        private static async Task<string> ReadLineAsync(NetworkStream stream, CancellationToken cancellationToken)
        {
            var sb = new StringBuilder();
            var one = new byte[1];
            while (true)
            {
                int read = await stream.ReadAsync(one, cancellationToken).ConfigureAwait(false);
                if (read == 0)
                    break;

                char c = (char)one[0];
                if (c == '\n')
                    break;
                if (c != '\r')
                    sb.Append(c);
            }
            return sb.ToString();
        }
    }
}
