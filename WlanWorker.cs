using System.Diagnostics;
using System.IO;

namespace GoProTimelapse
{
    public class WlanWorker
    {
        private readonly NetworkSettings _settings;

        public WlanWorker(NetworkSettings settings)
        {
            _settings = settings;
        }

        public void Connect(string ssid, string password)
        {
            string xmlProfile = GenerateXml(ssid, password);
            string tempPath = Path.Combine(Path.GetTempPath(), $"{ssid}_profile.xml");

            File.WriteAllText(tempPath, xmlProfile);
            ExecuteNetshCommand($"wlan add profile filename=\"{tempPath}\"");
            ExecuteNetshCommand($"wlan connect name=\"{ssid}\" ssid=\"{ssid}\"");

            File.Delete(tempPath);
        }

        private string GenerateXml(string ssid, string password) => $@"<?xml version=""1.0""?>
<WLANProfile xmlns=""http://www.microsoft.com/networking/WLAN/profile/v1"">
<name>{ssid}</name>
<SSIDConfig>
    <SSID>
    <name>{ssid}</name>
    </SSID>
</SSIDConfig>
<connectionType>ESS</connectionType>
<connectionMode>auto</connectionMode>
<MSM>
    <security>
    <authEncryption>
        <authentication>WPA2PSK</authentication>
        <encryption>AES</encryption>
        <useOneX>false</useOneX>
    </authEncryption>
    <sharedKey>
        <keyType>passPhrase</keyType>
        <protected>false</protected>
        <keyMaterial>{password}</keyMaterial>
    </sharedKey>
    </security>
</MSM>
</WLANProfile>";

        private void ExecuteNetshCommand(string arguments)
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "netsh",
                    Arguments = arguments,
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };
            process.Start();
            process.WaitForExit();
        }
    }
}
