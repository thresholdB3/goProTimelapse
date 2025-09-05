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
            string downloadFolder,
            int outputFps = 25)
        {
            string projectRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..\\..\\.."));
            string ffmpegPath = Path.Combine(AppContext.BaseDirectory, "ffmpeg.exe");
            string photosDirectory = Path.Combine(projectRoot, downloadFolder);

            var imageFiles = Directory.GetFiles(photosDirectory, "*.jpg");
            var sortedImageFiles = imageFiles
                .Select(f => new
                {
                    Path = f,
                    Number = int.TryParse(Path.GetFileNameWithoutExtension(f), out int num) ? num : -1
                })
                .Where(x => x.Number != -1)
                .OrderBy(x => x.Number)
                .Select(x => x.Path)
                .ToList();

            string inputListPath = Path.Combine(AppContext.BaseDirectory, "input.txt");
            File.WriteAllLines(inputListPath, sortedImageFiles.Select(f => $"file '{f.Replace("'", @"'\''")}'"));

            string outputFile = Path.Combine(projectRoot, DateTime.Now.ToString("ssmmhh.ddMMyyyy") + ".mp4");
            string arguments = $"-f concat -safe 0 -i \"{inputListPath}\" -c:v libx264 -r {outputFps} -pix_fmt yuv420p \"{outputFile}\"";

            using (var process = new Process())
            {
                var outputBuilder1 = new StringBuilder();
                var errorBuilder1 = new StringBuilder();
                process.StartInfo.FileName = ffmpegPath;
                process.StartInfo.Arguments = arguments;
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.RedirectStandardError = true;
                process.StartInfo.CreateNoWindow = true;
                //обработчики для чтения вывода
                process.OutputDataReceived += (sender, e) => outputBuilder1.AppendLine(e.Data);
                process.ErrorDataReceived += (sender, e) => errorBuilder1.AppendLine(e.Data);
                process.Start();
                //чтение вывода и ошибок, без этого всё зависает
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                process.WaitForExit();
            };

            File.Delete(inputListPath);
        }
    }
}
