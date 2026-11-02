using CommunityToolkit.Mvvm.ComponentModel;
using D3B.Bot.Settings;
using D3B.Data.Jobs;

namespace D3B.App.ViewModels.Settings;

/// <summary>Edits a setting of the professions that gather as chips: all the professions at once, each one, and <c>Base</c>'s apart (D165).</summary>
/// <remarks>The professions chosen are written in the game's order, <c>Base</c> last, whatever the order of the clicks.</remarks>
internal sealed partial class GatheringJobsFieldViewModel : SettingFieldViewModel
{
    /// <summary>Gets the chips: all the professions first, then each, <c>Base</c> last as "without a profession".</summary>
    [ObservableProperty]
    public partial IReadOnlyList<FilterChipViewModel> Chips { get; private set; } = [];

    /// <inheritdoc/>
    public override bool IsWide => true;

    /// <summary>Initializes a new instance of the <see cref="GatheringJobsFieldViewModel"/> class.</summary>
    /// <param name="definition">The setting.</param>
    /// <param name="editor">The editor it belongs to.</param>
    public GatheringJobsFieldViewModel(SettingDefinition definition, SettingsEditor editor)
        : base(definition, editor)
    {
        Name();
    }

    /// <inheritdoc/>
    public override void Relabel()
    {
        base.Relabel();
        Name();
    }

    /// <inheritdoc/>
    protected override void RefreshOptions()
    {
        Mark();
    }

    private void Name()
    {
        var chips = new List<FilterChipViewModel> { new(null, Editor.Localizer["AllJobs"], null, Toggle) };

        foreach (var job in Editor.Game?.GatheringJobs ?? [])
        {
            if (job.Id == Job.Base)
            {
                chips.Add(new FilterChipViewModel(job.Id, Editor.Localizer["NoJob"], null, Toggle) { IsBase = true });

                continue;
            }

            var option = Editor.JobOption(job.Id);

            chips.Add(new FilterChipViewModel(job.Id, option.Name, option.Icon, Toggle));
        }

        Chips = chips;
        Mark();
    }

    private void Mark()
    {
        var chosen = Definition.GetValues(Draft).Cast<int>().ToHashSet();
        var professions = Chips.Skip(1).Where(chip => !chip.IsBase).ToList();

        foreach (var chip in Chips.Skip(1))
            chip.IsSelected = chosen.Contains(chip.Id!.Value);

        Chips[0].IsSelected = professions.Count > 0 && professions.All(chip => chip.IsSelected);
    }

    private void Toggle(FilterChipViewModel chip)
    {
        var chosen = Definition.GetValues(Draft).Cast<int>().ToHashSet();

        if (chip.Id is { } id)
        {
            if (!chosen.Remove(id))
                chosen.Add(id);
        }
        else
        {
            // All the professions, Base's left as it is: off when every one was on, else on.
            List<int> professions = [.. Chips.Skip(1).Where(other => !other.IsBase).Select(other => other.Id!.Value)];

            if (professions.All(chosen.Contains))
                chosen.ExceptWith(professions);
            else
                chosen.UnionWith(professions);
        }

        Write(Chips.Skip(1).Select(other => other.Id!.Value).Where(chosen.Contains).Cast<object>().ToList());
        Mark();
    }
}
