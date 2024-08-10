using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
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
            Console.WriteLine("It's very possible for this script to crash, fail on some files, miss some, etc, as it uses GMAD. Expect issues! If you think it's taking a while, IT IS! Let it do its thing.");
            Console.WriteLine();

            if (args.Length > 0)
            {
                string targetDirectory = null;
                string outputDirectory = null;

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
                    string targetDirectory = Console.ReadLine();

                    if (Directory.Exists(targetDirectory))
                    {
                        string[] gmaFiles = Directory.GetFiles(targetDirectory, "*.gma", SearchOption.AllDirectories);
                        string exeLocation = AppDomain.CurrentDomain.BaseDirectory;
                        string newDirectoryPath = Path.Combine(exeLocation, "merged");

                        await ExtractAndMergeGMAsAsync(gmaFiles, newDirectoryPath);

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

        static async Task ExtractAndMergeGMAsAsync(string[] gmaFiles, string newDirectoryPath)
        {
            Stopwatch sw = new Stopwatch();
            sw.Start();

            if (!Directory.Exists(newDirectoryPath))
            {
                Directory.CreateDirectory(newDirectoryPath);
            }

            await Task.WhenAll(gmaFiles.Select(async gmaFile =>
            {
                string extractedFolder = Path.GetFileNameWithoutExtension(gmaFile);
                string addonFolderPath = Path.Combine(newDirectoryPath, extractedFolder);

                Directory.CreateDirectory(addonFolderPath);

                string gmadPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "gmad.exe");
                string gmadArguments = $"extract -file \"{gmaFile}\" -out \"{addonFolderPath}\"";

                ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    FileName = gmadPath,
                    Arguments = gmadArguments,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                using (Process process = new Process())
                {
                    process.StartInfo = startInfo;
                    process.Start();
                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();
                    await Task.Run(() => process.WaitForExit());
                }

                string[] extractedFiles = Array.Empty<string>();

                try
                {
                    extractedFiles = Directory.GetFiles(addonFolderPath, "*.*", SearchOption.AllDirectories);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error retrieving files from directory '{addonFolderPath}': {ex.Message}");
                }

                foreach (string file in extractedFiles)
                {
                    string relativePath = Path.GetRelativePath(addonFolderPath, file);
                    string targetFilePath = Path.Combine(newDirectoryPath, relativePath);

                    string targetDirectory = Path.GetDirectoryName(targetFilePath);
                    if (!Directory.Exists(targetDirectory))
                    {
                        Directory.CreateDirectory(targetDirectory);
                    }

                    try
                    {
                        if (File.Exists(file))
                        {
                            if (File.Exists(targetFilePath))
                            {
                                File.Delete(targetFilePath);
                            }

                            File.Move(file, targetFilePath);
                        }
                        else
                        {
                            Console.WriteLine($"Error: File '{file}' does not exist.");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error moving file '{file}': {ex.Message}");

                        System.Threading.Thread.Sleep(500);

                        try
                        {
                            if (File.Exists(file))
                            {
                                if (File.Exists(targetFilePath))
                                {
                                    File.Delete(targetFilePath);
                                }

                                File.Move(file, targetFilePath);
                                Console.WriteLine($"Successfully moved file '{file}' after retry.");
                            }
                            else
                            {
                                Console.WriteLine($"Error: File '{file}' does not exist after retry.");
                            }
                        }
                        catch (Exception retryEx)
                        {
                            Console.WriteLine($"Retry failed for moving file '{file}': {retryEx.Message}");
                        }
                    }
                }

                try
                {
                    Directory.Delete(addonFolderPath, true);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error deleting directory '{addonFolderPath}': {ex.Message}");
                }
            }));

            sw.Stop();
            Console.WriteLine($"Operation completed in {sw.Elapsed.TotalSeconds} seconds.");
        }
    }
}
