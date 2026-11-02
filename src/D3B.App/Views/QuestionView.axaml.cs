using Avalonia.Controls;

namespace D3B.App.Views;

/// <summary>A question asked in a dialog: its words, then its answers, the one that does it last.</summary>
internal sealed partial class QuestionView : UserControl
{
    /// <summary>Initializes a new instance of the <see cref="QuestionView"/> class.</summary>
    public QuestionView()
    {
        InitializeComponent();
    }
}
