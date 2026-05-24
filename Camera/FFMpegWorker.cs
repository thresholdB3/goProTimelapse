using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;
using Serilog;

namespace GoProTimelapse
{
    public static class FFMpegWorker
    {
        private static readonly ILogger Log = Serilog.Log.ForContext<Worker>(); //FFmpegWorker нельзя использовать ох чорт ох блин(((
        public static async Task CreateVideoFromPhotos(
            string downloadFolder,
            string outputFileName,
            int outputFps = 25)
        {
            try
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

                // string outputFile = Path.Combine(projectRoot, DateTime.Now.ToString("ssmmhh.ddMMyyyy") + ".mp4");
                string arguments = $"-f concat -safe 0 -i \"{inputListPath}\" -c:v libx264 -r {outputFps} -pix_fmt yuv420p \"{outputFileName}\"";

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
                }
                ;

                File.Delete(inputListPath);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Ошибка при создании видео :(");
            }
        }
        public static async Task<Stream> CompressAsync(string inputPath, CancellationToken cancellationToken = default) //todo: вроде не работает
            //пусть сюда будет идти щагрущка с диска
        {
            try
            {
                Log.Information("Начало сжатия видео");
                string ffmpegPath = Path.Combine(AppContext.BaseDirectory, "ffmpeg.exe");
                int crf = 29;
                int fps = 30;

                var psi = new ProcessStartInfo
                {
                    FileName = ffmpegPath,
                    Arguments =
                    $"-i \"{inputPath}\" " +
                    $"-c:v libx264 -preset veryslow -crf {crf} " +
                    $"-vf \"scale=1280:-2,setsar=1\" " +
                    $"-r {fps} " +
                    $"-an " +
                    $"-movflags frag_keyframe+empty_moov " + 
                    $"-f mp4 pipe:1",

                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using var process = new Process
                {
                    StartInfo = psi,
                    EnableRaisingEvents = true
                };

                process.Start();

                // Читаем stderr параллельно, иначе ffmpeg может зависнуть
                Task<string> errorTask = process.StandardError.ReadToEndAsync();

                var outputStream = new MemoryStream();

                // Читаем stdout параллельно
                Task copyTask = process.StandardOutput.BaseStream.CopyToAsync(
                    outputStream,
                    cancellationToken);

                await Task.WhenAll(copyTask, errorTask);

                await process.WaitForExitAsync(cancellationToken);

                string errors = await errorTask;

                if (process.ExitCode != 0)
                {
                    throw new Exception(
                        $"FFmpeg exited with code {process.ExitCode}\n{errors}");
                }

                outputStream.Position = 0;
                return outputStream;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Ошибка при сжатии видео :(");
                
                return null; //без этого ошибка
            }
        }
    }
}
