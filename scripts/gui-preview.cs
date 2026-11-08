#!/usr/bin/env dotnet
#:package System.CommandLine
#:project ../src/D3B.Gui/D3B.Gui.csproj
#:include gui-preview/GuiPreviewCommand.cs

using D3B.Scripts.GuiPreview;

return GuiPreviewCommand.Create().Parse(args).Invoke();
