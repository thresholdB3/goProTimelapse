using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace GoProTimelapse
{
    public static class FFMpegWorker
    {
        public static async Task CreateVideoFromPhotos(
            List<string> photos,
            string downloadFolder,
            int outputFps = 25)
        {
            string projectRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..\\..\\.."));
            string ffmpegPath = Path.Combine(AppContext.BaseDirectory, "ffmpeg.exe");

            string inputListPath = Path.Combine(AppContext.BaseDirectory, "input.txt");
            File.WriteAllLines(inputListPath, photos.Select(f => $"file '{f.Replace("'", @"'\''")}'"));

            string outputFile = Path.Combine(projectRoot, DateTime.Now.ToString("ssmmhh.ddMMyyyy") + ".mp4");
            string arguments = $"-f concat -safe 0 -i \"{inputListPath}\" -c:v libx264 -r {outputFps} -pix_fmt yuv420p \"{outputFile}\"";

            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = ffmpegPath,
                    Arguments = arguments,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                }
            };

            var outputBuilder = new StringBuilder();
            var errorBuilder = new StringBuilder();

            process.OutputDataReceived += (s, e) => outputBuilder.AppendLine(e.Data);
            process.ErrorDataReceived += (s, e) => errorBuilder.AppendLine(e.Data);

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            await Task.Run(() => process.WaitForExit());

            File.Delete(inputListPath);
        }
    }
}
