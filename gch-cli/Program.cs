using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ftlCLI
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("=====================================================");
            Console.WriteLine("            Meteors.gg - Game Hosting");
            Console.WriteLine("             https://meteors.gg");
            Console.WriteLine("              Licensed under MIT.");
            Console.WriteLine("=====================================================");
            Console.ResetColor();
            Console.WriteLine("This project was developed for & by the Meteors.gg team.");
            Console.WriteLine("Expect issues with GMAD extraction. Sit tight, it takes time.\n");

            string targetDirectory = null;
            string outputDirectory = null;

            if (args.Length > 0)
            {
                foreach (var arg in args)
                {
                    if (arg.StartsWith("-dir=", StringComparison.OrdinalIgnoreCase))
                    {
                        targetDirectory = arg.Substring(5);
                        Console.WriteLine($"Target Directory: {targetDirectory}");
                    }
                    else if (arg.StartsWith("-o=", StringComparison.OrdinalIgnoreCase))
                    {
                        outputDirectory = arg.Substring(3);
                        Console.WriteLine($"Output Directory: {outputDirectory}");
                    }
                }

                if (targetDirectory != null && outputDirectory != null)
                {
                    if (Directory.Exists(targetDirectory))
                    {
                        string[] gmaFiles = Directory.GetFiles(targetDirectory, "*.gma", SearchOption.AllDirectories);
                        await ExtractAndMergeGMAsAsync(gmaFiles, outputDirectory);
                        Console.WriteLine("Operation completed.");
                    }
                    else
                    {
                        Console.WriteLine("Directory does not exist.");
                    }
                }
                else
                {
                    Console.WriteLine("Usage: ftlCLI.exe -dir=<source_directory> -o=<output_directory>");
                }
            }
            else
            {
                Console.WriteLine("Select Mode:");
                Console.WriteLine("1. ftlCLI-fast (Create FastDL Folder, quickly, lots of CPU usage.)");

                string input = Console.ReadLine();
                if (input == "1")
                {
                    Console.WriteLine("Enter the target directory containing GMAs:");
                    targetDirectory = Console.ReadLine();

                    if (Directory.Exists(targetDirectory))
                    {
                        string[] gmaFiles = Directory.GetFiles(targetDirectory, "*.gma", SearchOption.AllDirectories);
                        string exeLocation = AppDomain.CurrentDomain.BaseDirectory;
                        outputDirectory = Path.Combine(exeLocation, "merged");

                        await ExtractAndMergeGMAsAsync(gmaFiles, outputDirectory);
                        Console.WriteLine("Operation completed.");
                    }
                    else
                    {
                        Console.WriteLine("Directory does not exist.");
                    }
                }
                else
                {
                    Console.WriteLine("Invalid selection or mode unavailable.");
                }
            }
        }

        static async Task ExtractAndMergeGMAsAsync(string[] gmaFiles, string outputDirectory)
        {
            var sw = Stopwatch.StartNew();
            Directory.CreateDirectory(outputDirectory);

            int maxConcurrency = Environment.ProcessorCount;
            using var semaphore = new SemaphoreSlim(maxConcurrency);

            var tasks = gmaFiles.Select(async gmaFile =>
            {
                await semaphore.WaitAsync();
                try
                {
                    string addonName = Path.GetFileNameWithoutExtension(gmaFile);
                    string addonFolderPath = Path.Combine(outputDirectory, addonName);
                    Directory.CreateDirectory(addonFolderPath);

                    string gmadPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "gmad.exe");
                    string arguments = $"extract -file \"{gmaFile}\" -out \"{addonFolderPath}\"";

                    var startInfo = new ProcessStartInfo
                    {
                        FileName = gmadPath,
                        Arguments = arguments,
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true
                    };

                    using var process = new Process { StartInfo = startInfo };
                    process.Start();
                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();
                    await process.WaitForExitAsync();

                    string[] extractedFiles;
                    try
                    {
                        extractedFiles = Directory.GetFiles(addonFolderPath, "*.*", SearchOption.AllDirectories);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error retrieving files from '{addonFolderPath}': {ex.Message}");
                        return;
                    }

                    foreach (var file in extractedFiles)
                    {
                        string relativePath = Path.GetRelativePath(addonFolderPath, file);
                        string targetFilePath = Path.Combine(outputDirectory, relativePath);
                        Directory.CreateDirectory(Path.GetDirectoryName(targetFilePath));

                        try
                        {
                            if (File.Exists(targetFilePath))
                                File.Delete(targetFilePath);
                            File.Move(file, targetFilePath);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error moving '{file}': {ex.Message}. Retrying...");
                            await Task.Delay(500);
                            try
                            {
                                if (File.Exists(targetFilePath))
                                    File.Delete(targetFilePath);
                                File.Move(file, targetFilePath);
                                Console.WriteLine($"Moved '{file}' on retry.");
                            }
                            catch (Exception retryEx)
                            {
                                Console.WriteLine($"Retry failed for '{file}': {retryEx.Message}");
                            }
                        }
                    }

                    try
                    {
                        Directory.Delete(addonFolderPath, true);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error deleting '{addonFolderPath}': {ex.Message}");
                    }
                }
                finally
                {
                    semaphore.Release();
                }
            });

            await Task.WhenAll(tasks);
            sw.Stop();
            Console.WriteLine($"Operation completed in {sw.Elapsed.TotalSeconds} seconds.");
        }
    }
}
