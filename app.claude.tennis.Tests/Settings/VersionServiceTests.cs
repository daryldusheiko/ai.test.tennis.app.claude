using System.Net;
using SocialTennis.Settings;

namespace SocialTennis.Tests.Settings;

public class VersionServiceTests
{
    private static VersionService Create(HttpStatusCode status, string body) =>
        new(new HttpClient(new FakeHandler(status, body)) { BaseAddress = new Uri("http://localhost/") });

    [Test]
    public async Task GetAsync_StampedFile_ReadsTheVersion()
    {
        var service = Create(HttpStatusCode.OK, /* lang=json,strict */ "{\"version\":\"1.0.42\",\"commit\":\"abc1234\",\"released\":\"2026-03-07\"}");

        var info = await service.GetAsync();

        Assert.That(info, Is.EqualTo(new VersionInfo("1.0.42", "abc1234", "2026-03-07")));
    }

    [Test]
    public async Task GetAsync_MissingFile_IsTheDevelopmentVersion() =>
        Assert.That(await Create(HttpStatusCode.NotFound, "no").GetAsync(), Is.EqualTo(VersionInfo.Development));

    [Test]
    public async Task GetAsync_UnreadableFile_IsTheDevelopmentVersion() =>
        Assert.That(await Create(HttpStatusCode.OK, "<html>").GetAsync(), Is.EqualTo(VersionInfo.Development));

    [Test]
    public async Task GetAsync_FileWithNoVersion_IsTheDevelopmentVersion() =>
        Assert.That(await Create(HttpStatusCode.OK, "{}").GetAsync(), Is.EqualTo(VersionInfo.Development));

    private sealed class FakeHandler(HttpStatusCode status, string body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body) });
    }
}
