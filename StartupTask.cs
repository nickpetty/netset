using System.Diagnostics;
using System.Security;
using System.Security.Principal;
using System.Text;

namespace NetSet;

/// <summary>
/// Run-at-logon via a Task Scheduler task. A plain Run registry entry would not work:
/// NetSet requires administrator rights, and Windows skips elevated apps in Run keys.
/// A task with "highest privileges" starts elevated without a UAC prompt.
/// </summary>
public static class StartupTask
{
    const string TaskName = "NetSet";

    public static bool IsEnabled => Schtasks("/Query", "/TN", TaskName).code == 0;

    public static (bool ok, string output) Set(bool enable)
    {
        if (!enable) return Ok(Schtasks("/Delete", "/TN", TaskName, "/F"));

        string xmlPath = Path.Combine(Path.GetTempPath(), $"netset-task-{Guid.NewGuid():N}.xml");
        try
        {
            File.WriteAllText(xmlPath, BuildXml(), Encoding.Unicode);
            return Ok(Schtasks("/Create", "/TN", TaskName, "/XML", xmlPath, "/F"));
        }
        finally
        {
            try { File.Delete(xmlPath); } catch { }
        }
    }

    static (bool, string) Ok((int code, string output) r) => (r.code == 0, r.output.Trim());

    static string BuildXml()
    {
        string user = SecurityElement.Escape(WindowsIdentity.GetCurrent().Name) ?? "";
        string exe = SecurityElement.Escape(Environment.ProcessPath ?? "") ?? "";
        return $"""
            <?xml version="1.0" encoding="UTF-16"?>
            <Task version="1.2" xmlns="http://schemas.microsoft.com/windows/2004/02/mit/task">
              <RegistrationInfo><Description>Starts NetSet in the system tray at logon.</Description></RegistrationInfo>
              <Triggers>
                <LogonTrigger><Enabled>true</Enabled><UserId>{user}</UserId></LogonTrigger>
              </Triggers>
              <Principals>
                <Principal id="Author"><UserId>{user}</UserId><LogonType>InteractiveToken</LogonType><RunLevel>HighestAvailable</RunLevel></Principal>
              </Principals>
              <Settings>
                <MultipleInstancesPolicy>IgnoreNew</MultipleInstancesPolicy>
                <DisallowStartIfOnBatteries>false</DisallowStartIfOnBatteries>
                <StopIfGoingOnBatteries>false</StopIfGoingOnBatteries>
                <ExecutionTimeLimit>PT0S</ExecutionTimeLimit>
                <AllowStartOnDemand>true</AllowStartOnDemand>
                <Enabled>true</Enabled>
              </Settings>
              <Actions Context="Author"><Exec><Command>{exe}</Command><Arguments>--startup</Arguments></Exec></Actions>
            </Task>
            """;
    }

    static (int code, string output) Schtasks(params string[] args)
    {
        var psi = new ProcessStartInfo("schtasks.exe")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (var a in args) psi.ArgumentList.Add(a);
        using var p = Process.Start(psi)!;
        string text = p.StandardOutput.ReadToEnd() + p.StandardError.ReadToEnd();
        p.WaitForExit();
        return (p.ExitCode, text);
    }
}
