using XenoAtom.Terminal.UI;

namespace CodeAlta.ViewModels;

internal sealed partial class CodeAltaShellViewModel
{
    public CodeAltaShellViewModel()
    {
        HeaderText = "CodeAlta";
        StatusText = "Prompt ready";
        StatusIconMarkup = string.Empty;
        StatusTone = CodeAltaApp.StatusTone.Ready;
    }

    [Bindable]
    public partial string HeaderText { get; set; }

    [Bindable]
    public partial string StatusText { get; set; }

    [Bindable]
    public partial string StatusIconMarkup { get; set; }

    [Bindable]
    public partial bool StatusBusy { get; set; }

    [Bindable]
    public partial CodeAltaApp.StatusTone StatusTone { get; set; }
}
