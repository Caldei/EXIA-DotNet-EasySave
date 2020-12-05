﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using EasySave.NS_Model;

namespace EasySave.NS_ViewModel
{
    class MenuViewModel : ViewModel
    {
        // ----- Methods -----
        public int RemoveWork(int _index)
        {
            try
            {
                // Remove Work from the program (at index)
                this.works.RemoveAt(_index);
                SaveWorks();

                // Return Success Code
                return 103;
            }
            catch
            {
                // Return Error Code
                return 203;
            }
        }

        public void LaunchBackupWork(int[] idWorkToSave)
        {
            int businessSoftwaresId;

            if (this.works.Count > 0 && idWorkToSave.Length > 0)
            {
                for (int i = 0; i < idWorkToSave.Length; i++)
                {
                    // Get id of one Running Business Software from the List (-1 if none) 
                    businessSoftwaresId = this.settings.businessSoftwares.FindIndex(x => Process.GetProcessesByName(x).Length > 0);

                    // Prevents from Lauching Backups if one Business Software is Running
                    if (businessSoftwaresId != -1)
                    {
                        Console.WriteLine($"{this.settings.businessSoftwares[businessSoftwaresId]} is running"); // ---- TODO : Handle Error Message in View ---- //
                    }
                    else
                    {
                        LaunchBackupType(this.works[idWorkToSave[i]]);
                        // this.view.ConsoleUpdate(LaunchBackupType(this.works[idWorkToSave[i]]));
                        // this.view.ConsoleUpdate(4);
                    }
                }

                // Retour menu 
                //this.view.ConsoleUpdate(1);
            }
            else
            {
                // PAS DE WORK
                // this.view.ConsoleUpdate(204);
            }
        }


        public int LaunchBackupType(Work _work)
        {
            DirectoryInfo dir = new DirectoryInfo(_work.src);

            // Check if the source & destionation folder exists
            if (!dir.Exists && !Directory.Exists(_work.dst))
            {
                // Return Error Code
                return 207;
            }

            // Run the correct backup (Full or Diff)
            switch (_work.backupType)
            {
                case BackupType.DIFFRENTIAL:
                    string fullBackupDir = GetFullBackupDir(_work);

                    // If there is no first full backup, we create the first one (reference of the next diff backup)
                    if (fullBackupDir != null)
                    {
                        return DifferentialBackupSetup(_work, dir, fullBackupDir);
                    }
                    return FullBackupSetup(_work, dir);

                case BackupType.FULL:
                    return FullBackupSetup(_work, dir);

                default:
                    // Return Error Code
                    return 208;
            }
        }

        // Get the directory of the first full backup of a differential backup
        private string GetFullBackupDir(Work _work)
        {
            // Get all directories name of the dest folder
            DirectoryInfo[] dirs = new DirectoryInfo(_work.dst).GetDirectories();

            foreach (DirectoryInfo directory in dirs)
            {
                if (directory.Name.IndexOf("_") > 0 && _work.name == directory.Name.Substring(0, directory.Name.IndexOf("_")))
                {
                    return directory.FullName;
                }
            }
            return null;
        }

        // Full Backup
        private int FullBackupSetup(Work _work, DirectoryInfo _dir)
        {
            long totalSize = 0;

            // Get evvery files of the source directory
            FileInfo[] files = _dir.GetFiles("*.*", SearchOption.AllDirectories);

            // Calcul the size of every files
            foreach (FileInfo file in files)
            {
                totalSize += file.Length;
            }
            return DoBackup(_work, files, totalSize);
        }

        // Differential Backup
        private int DifferentialBackupSetup(Work _work, DirectoryInfo _dir, string _fullBackupDir)
        {
            long totalSize = 0;

            // Get evvery files of the source directory
            FileInfo[] srcFiles = _dir.GetFiles("*.*", SearchOption.AllDirectories);
            List<FileInfo> filesToCopy = new List<FileInfo>();

            // Check if there is a modification between the current file and the last full backup
            foreach (FileInfo file in srcFiles)
            {
                string currFullBackPath = _fullBackupDir + "\\" + Path.GetRelativePath(_work.src, file.FullName);

                if (!File.Exists(currFullBackPath) || !IsSameFile(currFullBackPath, file.FullName))
                {
                    // Calcul the size of every files
                    totalSize += file.Length;

                    // Add the file to the list
                    filesToCopy.Add(file);
                }
            }

            // Test if there is file to copy
            if (filesToCopy.Count == 0)
            {
                _work.lastBackupDate = DateTime.Now.ToString("yyyy/MM/dd_HH:mm:ss");
                this.SaveWorks();
                // this.view.ConsoleUpdate(3);
               // this.view.DisplayBackupRecap(_work.name, 0);
                return 105;
            }
            return DoBackup(_work, filesToCopy.ToArray(), totalSize);
        }

        // Check if the file or the src is the same as the full backup one to know if the files need to be copied or not
        private bool IsSameFile(string path1, string path2)
        {
            byte[] file1 = File.ReadAllBytes(path1);
            byte[] file2 = File.ReadAllBytes(path2);

            if (file1.Length == file2.Length)
            {
                for (int i = 0; i < file1.Length; i++)
                {
                    if (file1[i] != file2[i])
                    {
                        return false;
                    }
                }
                return true;
            }
            return false;
        }

        // Do Backup
        private int DoBackup(Work _work, FileInfo[] _files, long _totalSize)
        {
            // Create the state file
            DateTime startTime = DateTime.Now;
            string dst = _work.dst + _work.name + "_" + startTime.ToString("yyyy-MM-dd_HH-mm-ss") + "\\";

            // Update the current work status
            _work.state = new State(_files.Length, _totalSize, _work.src, dst);
            _work.lastBackupDate = startTime.ToString("yyyy/MM/dd_HH:mm:ss");

            // Create the dst folder
            try
            {
                Directory.CreateDirectory(dst);
            }
            catch
            {
                return 210;
            }
            List<string> failedFiles = CopyFiles(_work, _files, _totalSize, dst);

            // Calculate the time of the all process of copy
            DateTime endTime = DateTime.Now;
            TimeSpan workTime = endTime - startTime;
            double transferTime = workTime.TotalMilliseconds;

            // Update the current work status
            _work.state = null;
            this.SaveWorks();
            //this.view.ConsoleUpdate(3);

            foreach (string failedFile in failedFiles)
            {
                //this.view.DisplayFiledError(failedFile);
            }
            //this.view.DisplayBackupRecap(_work.name, transferTime);

            if (failedFiles.Count == 0)
            {
                // Return Success Code
                return 104;
            }
            else
            {
                // Return Error Code
                return 216;
            }
        }

        private List<string> CopyFiles(Work _work, FileInfo[] _files, long _totalSize, string _dst)
        {
            // Files Size
            long leftSize = _totalSize;
            // Number of Files
            int totalFile = _files.Length;
            List<string> failedFiles = new List<string>();

            // Copy every file
            for (int i = 0; i < _files.Length; i++)
            {
                // Update the size remaining to copy (octet)
                int pourcent = (i * 100 / totalFile);
                long curSize = _files[i].Length;
                leftSize -= curSize;

                if (this.CopyFile(_work, _files[i], curSize, _dst, leftSize, totalFile, i, pourcent))
                {
                    // this.view.DisplayCurrentState(_work.name, (totalFile - i), leftSize, curSize, pourcent);
                }
                else
                {
                    failedFiles.Add(_files[i].Name);
                }
            }
            return failedFiles;
        }

        public bool CopyFile(Work _work, FileInfo _currentFile, long _curSize, string _dst, long _leftSize, int _totalFile, int fileIndex, int _pourcent)
        {
            // Time at when file copy start (use by SaveLog())
            DateTime startTimeFile = DateTime.Now;
            string curDirPath = _currentFile.DirectoryName;
            string dstDirectory = _dst;

            // If there is a directoy, we add the relative path from the directory dst
            if (Path.GetRelativePath(_work.src, curDirPath).Length > 1)
            {
                dstDirectory += Path.GetRelativePath(_work.src, curDirPath) + "\\";

                // If the directory dst doesn't exist, we create it
                if (!Directory.Exists(dstDirectory))
                {
                    Directory.CreateDirectory(dstDirectory);
                }
            }

            // Get the current dstFile
            string dstFile = dstDirectory + _currentFile.Name;

            try
            {
                // Update the current work status
                _work.state.UpdateState(_pourcent, (_totalFile - fileIndex), _leftSize, _currentFile.FullName, dstFile);
                SaveWorks();

                // Copy the current file
                _currentFile.CopyTo(dstFile, true);

                // Save Log
                _work.SaveLog(startTimeFile, _currentFile.FullName, dstFile, _curSize, false);
                return true;
            }
            catch
            {
                _work.SaveLog(startTimeFile, _currentFile.FullName, dstFile, _curSize, true);
                return false;
            }
        }
    }
}