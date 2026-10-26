using SocialTennis.Scheduling;
using SocialTennis.Tests.Players;

namespace SocialTennis.Tests.Scheduling;

public class ScheduleServiceTests
{
    [Test]
    public async Task GetAsync_NothingStored_ReturnsNull()
    {
        var service = new ScheduleService(new InMemoryStorage());

        Assert.That(await service.GetAsync(), Is.Null);
    }

    [Test]
    public async Task SaveAsync_ThenGet_RoundTripsEverything()
    {
        var service = new ScheduleService(new InMemoryStorage());
        var generated = ScheduleFlow.Generate(
            ScheduleFlow.WithPlayers(ScheduleFlow.WithCourts(ScheduleFlow.Start(Guid.NewGuid()), [4, 5]), TestPlayers.Mixed(9)),
            [],
            3);

        await service.SaveAsync(generated);
        var loaded = await service.GetAsync();

        Assert.That(loaded, Is.Not.Null);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(loaded.Id, Is.EqualTo(generated.Id));
            Assert.That(loaded.Step, Is.EqualTo(ScheduleStep.Review));
            Assert.That(loaded.Courts, Is.EqualTo(generated.Courts));
            Assert.That(loaded.Players, Is.EqualTo(generated.Players));
            Assert.That(loaded.Rounds, Has.Count.EqualTo(generated.Rounds.Count));
            Assert.That(loaded.Rounds[2].Games[0].Team1, Is.EqualTo(generated.Rounds[2].Games[0].Team1));
            Assert.That(loaded.Rounds[2].SittingOut, Is.EqualTo(generated.Rounds[2].SittingOut));
            Assert.That(loaded.Seed, Is.EqualTo(3));
        }
    }

    [Test]
    public async Task SaveAsync_StepStoredAsText_SoLaterVersionsCanReadIt()
    {
        var storage = new InMemoryStorage();

        await new ScheduleService(storage).SaveAsync(ScheduleFlow.Start(Guid.NewGuid()));

        Assert.That(storage.Values[ScheduleService.ScheduleKey], Does.Contain("\"Courts\""));
    }

    [Test]
    public async Task GetAsync_UnreadableData_ReturnsNull()
    {
        var storage = new InMemoryStorage();
        storage.Values[ScheduleService.ScheduleKey] = "{not json";

        Assert.That(await new ScheduleService(storage).GetAsync(), Is.Null);
    }

    [Test]
    public async Task GetAsync_FieldsMissingAndUnknownFieldsPresent_LoadsWithEmptyLists()
    {
        var storage = new InMemoryStorage();
        storage.Values[ScheduleService.ScheduleKey] = /*lang=json,strict*/ """{"Id":"00000000-0000-0000-0000-000000000001","Step":"Players","FutureField":1}""";

        var loaded = await new ScheduleService(storage).GetAsync();

        Assert.That(loaded, Is.Not.Null);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(loaded.Step, Is.EqualTo(ScheduleStep.Players));
            Assert.That(loaded.Courts, Is.Empty);
            Assert.That(loaded.Players, Is.Empty);
            Assert.That(loaded.Rounds, Is.Empty);
        }
    }

    [Test]
    public async Task SaveAsync_PinsChangesAndAcceptedPairs_RoundTrip()
    {
        var service = new ScheduleService(new InMemoryStorage());
        var generated = ScheduleFlow.Generate(
            ScheduleFlow.WithPlayers(ScheduleFlow.WithCourts(ScheduleFlow.Start(Guid.NewGuid()), [1, 2]), TestPlayers.Mixed(8)),
            [],
            3);
        var first = generated.Rounds[0].Games[0].Team1[0];
        var second = generated.Rounds[0].Games[0].Team2[0];
        var edited = ScheduleEditor.Pin(generated, new PlayerSpot(0, first)).Schedule;
        edited = ScheduleEditor.Swap(edited, new PlayerSpot(1, generated.Rounds[1].Games[0].Team1[0]), new PlayerSpot(1, generated.Rounds[1].Games[1].Team1[0])).Schedule;
        edited = ScheduleEditor.AcceptPair(edited, PlayerPair.Of(first, second)).Schedule;

        await service.SaveAsync(edited);
        var loaded = await service.GetAsync();

        Assert.That(loaded, Is.Not.Null);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(loaded.Rounds[0].Pinned.Single(), Is.EqualTo(first));
            Assert.That(loaded.Rounds[1].Changed, Is.EqualTo(edited.Rounds[1].Changed));
            Assert.That(loaded.Rounds[1].Changed, Has.Count.EqualTo(2));
            Assert.That(loaded.AcceptedPairs.Single(), Is.EqualTo(PlayerPair.Of(first, second)));
        }
    }

    [Test]
    public async Task GetAsync_ScheduleSavedBeforeEditing_LoadsWithNoPinsChangesOrAcceptedPairs()
    {
        var storage = new InMemoryStorage();
        storage.Values[ScheduleService.ScheduleKey] = /*lang=json,strict*/ """{"Id":"00000000-0000-0000-0000-000000000001","Step":"Review","Courts":[1],"Players":[],"Rounds":[{"Games":[],"SittingOut":[]}],"Seed":1}""";

        var loaded = await new ScheduleService(storage).GetAsync();

        Assert.That(loaded, Is.Not.Null);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(loaded.Rounds[0].Pinned, Is.Empty);
            Assert.That(loaded.Rounds[0].Changed, Is.Empty);
            Assert.That(loaded.AcceptedPairs, Is.Empty);
        }
    }

    [Test]
    public async Task DeleteAsync_Stored_LeavesNoSchedule()
    {
        var service = new ScheduleService(new InMemoryStorage());
        await service.SaveAsync(ScheduleFlow.Start(Guid.NewGuid()));

        await service.DeleteAsync();

        Assert.That(await service.GetAsync(), Is.Null);
    }
}
