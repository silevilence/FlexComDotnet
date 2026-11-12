using System.IO;
using System.Xml.Linq;
using FlexComDotnet.Core.Features.Layout.Models;
using FlexComDotnet.Features.Layout.Controls;

namespace FlexComDotnet.Features.Layout.Services;

/// <summary>Versioned layout storage, independent of the application's business configuration.</summary>
public sealed class DockLayoutStore(string path)
{
    public bool Load(DockWorkspace workspace, LayoutState legacy, string? configPath = null)
    {
        try
        {
            if (!File.Exists(path))
            {
                if (configPath != null && File.Exists(configPath) && !File.Exists(configPath + ".before-docking"))
                    File.Copy(configPath, configPath + ".before-docking");
                workspace.InitializeLayout(legacy);
                return true;
            }
            workspace.ResetLayout();
            if (workspace.RestoreLayout(File.ReadAllText(path))) return true;
            if (!File.Exists(path + ".invalid")) File.Copy(path, path + ".invalid");
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            System.Diagnostics.Trace.TraceWarning("Cannot load layout: {0}", error.Message);
        }
        workspace.ResetLayout();
        return false;
    }

    public void Save(DockWorkspace workspace)
    {
        var document = XDocument.Parse(workspace.SaveLayout());
        document.Root!.SetAttributeValue("FlexComVersion", "1");
        var directory = Path.GetDirectoryName(Path.GetFullPath(path))!;
        Directory.CreateDirectory(directory);
        // Write completely before replacing the last usable layout.
        File.WriteAllText(path + ".tmp", document.ToString());
        File.Move(path + ".tmp", path, true);
    }
}
