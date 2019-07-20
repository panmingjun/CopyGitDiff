using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;

namespace CopyGitDiff
{
    class Program
    {
        static void Main(string[] args)
        {
            try
            {
                IConfiguration configuration = new ConfigurationBuilder().AddIniFile("config.ini")
                    .Build();
                string workDir = configuration.GetSection("ProjectPath").Value;
                string targetDir = configuration.GetSection("TargetPath").Value;
                string masterCommit = configuration.GetSection("CompareCommit").Value;
                string exts = configuration.GetSection("Exts").Value;
                if (string.IsNullOrWhiteSpace(exts))
                {
                    throw new ArgumentException("Exts 不能为空");
                }
                bool clearTarget = configuration.GetSection("ClearTarget").Value?.Trim() == "1" ? true : false;
                List<string> diffFile = new List<string>();


                using (Process git = new Process())
                {
                    git.StartInfo.WorkingDirectory = workDir;
                    git.StartInfo.UseShellExecute = false;
                    git.StartInfo.RedirectStandardOutput = true;
                    git.StartInfo.FileName = "git";


                    //获取master的commit
                    if (string.IsNullOrEmpty(masterCommit))
                    {
                        git.StartInfo.Arguments = " log -n 1 master";
                        git.Start();

                        var output = git.StandardOutput.ReadToEnd();
                        var regex = new Regex("commit (.*?)\n");
                        var m = regex.Match(output);
                        if (m.Groups.Count >= 2)
                        {
                            masterCommit = m.Groups[1].Value;
                        }
                        git.Close();
                    }

                    //获取差异文件列表
                    if (!string.IsNullOrWhiteSpace(masterCommit))
                    {
                        git.StartInfo.Arguments = $" diff {masterCommit} --name-only";
                        git.Start();

                        Regex regex = new Regex($@".*?\.{exts}$");

                        while (true)
                        {
                            var output = git.StandardOutput.ReadLine();
                            if (string.IsNullOrWhiteSpace(output))
                                break;
                            if (regex.IsMatch(output))
                                diffFile.Add(output);
                            else
                                continue;
                        }
                    }
                    if (clearTarget)
                    {
                        if (Directory.Exists(targetDir))
                        {
                            Directory.Delete(targetDir, true);
                            Directory.CreateDirectory(targetDir);
                        }
                    }
                    //复制文件
                    if (diffFile.Any())
                    {
                        Regex fileRegex = new Regex(@"(.*)[\\/](.*?)$");
                        foreach (var item in diffFile)
                        {
                            var m = fileRegex.Match(item);
                            if (m.Length < 3)
                                continue;
                            string path = m.Groups[1].Value;
                            string name = m.Groups[2].Value;
                            if (!Directory.Exists(path))
                            {
                                Directory.CreateDirectory(targetDir + "/" + path);
                            }
                            File.Copy(workDir + "/" + item, targetDir + "/" + item, true);
                        }
                    }
                    //git.WaitForExit();
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }
        }
    }
}