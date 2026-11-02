using System.Globalization;
using D3B.Bot.Activities;
using D3B.Bot.Localization;
using D3B.Bot.Tests.Runtime;
using D3B.Bot.Tests.Settings;
using Microsoft.Extensions.DependencyInjection;

namespace D3B.Storage.Tests.Profiles;

public sealed class ProfileBookTests : IAsyncDisposable
{
    private readonly TestStorage _storage = new();

    public ValueTask DisposeAsync()
    {
        return _storage.DisposeAsync();
    }

    [Fact]
    public async Task AProfileIsSavedThenUpdatedAndTheProfilesReadByName()
    {
        await _storage.StartedAsync();

        var cancellationToken = TestContext.Current.CancellationToken;
        var version = _storage.Profiles.Version;
        var stroll = await _storage.Profiles.SaveAsync(0, "  stroll ", TestStorage.PlainActivityId, "{}", cancellationToken);
        var lumberjack = await _storage.Profiles.SaveAsync(0, "Lumberjack", TestStorage.SettingsActivityId, """{"Walks":3}""", cancellationToken);

        Assert.Equal("stroll", stroll.Name);
        Assert.NotEqual(stroll.Id, lumberjack.Id);
        Assert.Equal(["Lumberjack", "stroll"], _storage.Profiles.Profiles.Select(profile => profile.Name));
        Assert.Equal(version + 2, _storage.Profiles.Version);

        _storage.Time.Advance(TimeSpan.FromMinutes(5));

        var updated = await _storage.Profiles.SaveAsync(stroll.Id, "Stroll", TestStorage.SettingsActivityId, """{"Walks":9}""", cancellationToken);

        Assert.Equal(stroll.Id, updated.Id);
        Assert.Equal(("Stroll", TestStorage.SettingsActivityId, """{"Walks":9}"""), (updated.Name, updated.ActivityId, updated.Settings));
        Assert.Equal(_storage.Time.GetUtcNow(), updated.Updated);
        Assert.Same(updated, _storage.Profiles.Find(stroll.Id));
    }

    [Fact]
    public async Task AProfilesNameIsUniqueWhateverItsCaseNotBlankAndShort()
    {
        await _storage.StartedAsync();

        var cancellationToken = TestContext.Current.CancellationToken;
        var stroll = await _storage.Profiles.SaveAsync(0, "Stroll", TestStorage.PlainActivityId, "{}", cancellationToken);

        Assert.True(_storage.Profiles.IsTaken(" STROLL "));
        Assert.False(_storage.Profiles.IsTaken("stroll", stroll.Id));
        await Assert.ThrowsAsync<ArgumentException>(() => _storage.Profiles.SaveAsync(0, "STROLL", TestStorage.PlainActivityId, "{}", cancellationToken));
        await Assert.ThrowsAsync<ArgumentException>(() => _storage.Profiles.SaveAsync(0, "  ", TestStorage.PlainActivityId, "{}", cancellationToken));
        await Assert.ThrowsAsync<ArgumentException>(() => _storage.Profiles.SaveAsync(0, new string('a', 61), TestStorage.PlainActivityId, "{}", cancellationToken));

        Assert.Single(_storage.Profiles.Profiles);
    }

    [Fact]
    public async Task DeletingAProfileLeavesItsCharactersWithoutOne()
    {
        await _storage.StartedAsync();
        await _storage.Play.EnterAsync(300);
        await _storage.RecordedAsync();

        var cancellationToken = TestContext.Current.CancellationToken;
        var stroll = await _storage.Profiles.SaveAsync(0, "Stroll", TestStorage.PlainActivityId, "{}", cancellationToken);

        await _storage.Accounts.AssignAsync(TestPlay.Key, stroll.Id, cancellationToken);
        await _storage.Accounts.SetStartsOnConnectionAsync(TestPlay.Key, true, cancellationToken);

        var version = _storage.Accounts.Version;

        await _storage.Profiles.DeleteAsync(stroll.Id, cancellationToken);

        var character = _storage.Accounts.Find(TestPlay.Key);

        Assert.Empty(_storage.Profiles.Profiles);
        Assert.Equal((null, false), (character?.ProfileId, character?.StartsOnConnection));
        Assert.True(_storage.Accounts.Version > version);
    }

    [Fact]
    public async Task APresetIsStoredOnceAsAProfileNamedApartFromTheUsersAndNeverAgainOnceDeleted()
    {
        await _storage.StartedAsync();

        var cancellationToken = TestContext.Current.CancellationToken;
        var services = new ServiceCollection();

        services.AddActivity<SettingsActivity, TestSettings>("test.presets", LocalizedText.Invariant("Presets"), settings =>
        {
            settings.Number(s => s.Walks, LocalizedText.Invariant("Walks"), 1, 100);
            settings.Preset("long", LocalizedText.Invariant("Long walk"), s => s.Walks = 50);
            settings.Preset("short", LocalizedText.Invariant("Short walk"), s => s.Walks = 2);
        });

        var activity = Assert.Single(services.Select(descriptor => descriptor.ImplementationInstance).OfType<ActivityDefinition>());

        await _storage.Profiles.SaveAsync(0, "Long walk", TestStorage.PlainActivityId, "{}", cancellationToken);

        Assert.Equal(2, await _storage.Profiles.StorePresetsAsync([activity], CultureInfo.InvariantCulture, cancellationToken));
        Assert.Equal(["Long walk", "Long walk 2", "Short walk"], _storage.Profiles.Profiles.Select(profile => profile.Name));

        var stored = _storage.Profiles.Profiles.Single(profile => profile.Name == "Short walk");

        Assert.Equal(("test.presets", """{"Walks":2}"""), (stored.ActivityId, stored.Settings));

        await _storage.Profiles.DeleteAsync(stored.Id, cancellationToken);

        Assert.Equal(0, await _storage.Profiles.StorePresetsAsync([activity], CultureInfo.InvariantCulture, cancellationToken));
        Assert.DoesNotContain(_storage.Profiles.Profiles, profile => profile.Name == "Short walk");
    }
}
