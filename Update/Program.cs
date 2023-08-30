using Ionic.Zip;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Update
{
    class Program
    {
        static void Main(string[] args)
        {
            string exeFolder = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);

            Console.Title = "SB-Prime Update";
            Console.WriteLine("Updating SB-Prime...");
            Console.WriteLine("Please close all instances of SB-Prime...");
            int i = 0;
            while (Process.GetProcessesByName("SB-Prime").Count() > 0)
            {
                if (i == 0) Console.WriteLine("Waiting for SB-Prime to close...");
                Thread.Sleep(100);
                i++;
            }
            Updater updater = new Updater(exeFolder);
            updater.Update("http://litdev.uk/downloads/SB-Prime.zip");
            Console.WriteLine("Restarting SB-Prime...");
            Thread.Sleep(100);
            Process.Start(exeFolder + "\\SB-Prime.exe");
        }
    }

    public class Updater
    {
        [DllImport("kernel32", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool DeleteFile(string name);

        private string exeFolder;

        public Updater(string exeFolder)
        {
            this.exeFolder = exeFolder;
        }

        private bool Unblock(string fileName)
        {
            return DeleteFile(fileName + ":Zone.Identifier");
        }

        public void Update(string SBPrimeURL)
        {
            string tempFolder = GetTempFolder();
            string tempZip = Path.GetTempFileName();
            try
            {
                Console.WriteLine("Downloading new files...");
                DownloadZip(SBPrimeURL, tempZip);
                Console.WriteLine("Unblocking downloaded files...");
                Unblock(tempZip);
                Console.WriteLine("Replacing downloaded files...");
                UnZip(tempZip, tempFolder);
                CopyFiles(exeFolder, tempFolder);
                foreach (string subFolder in Directory.GetDirectories(tempFolder))
                {
                    string langPath = exeFolder + "\\" + new DirectoryInfo(subFolder).Name;
                    if (!Directory.Exists(langPath)) Directory.CreateDirectory(langPath);
                    CopyFiles(langPath, subFolder);
                }
                string langFile = exeFolder + "\\SB-Prime.resources.dll";
                if (File.Exists(langFile)) File.Delete(langFile);
                File.Delete(tempZip);
                Directory.Delete(tempFolder, true);
            }
            catch (Exception ex)
            {
                File.Delete(tempZip);
                Directory.Delete(tempFolder, true);
                Console.WriteLine("Error : " + ex.Message);
                Console.WriteLine("Press a key to close");
                Console.ReadKey(true);
            }
        }

        private string GetTempFolder()
        {
            int SBEnum = 0;
            string folder = Path.Combine(Path.GetTempPath(), "SBPrime" + (SBEnum++).ToString());
            while (Directory.Exists(folder) || File.Exists(folder))
            {
                folder = Path.Combine(Path.GetTempPath(), "SBPrime" + (SBEnum++).ToString());
            }
            return folder;
        }

        private void DownloadZip(string zipURL, string zipFile)
        {
            FileInfo fileInf = new FileInfo(zipFile);
            Uri uri = new Uri(zipURL);
            WebRequest.DefaultWebProxy.Credentials = CredentialCache.DefaultCredentials;
            HttpWebRequest webRequest = (HttpWebRequest)WebRequest.Create(uri);

            int bufferSize = 2048;
            byte[] buffer = new byte[bufferSize];

            FileStream fs = fileInf.OpenWrite();
            WebResponse webResponse = webRequest.GetResponse();
            Stream stream = webResponse.GetResponseStream();

            int readCount;
            do
            {
                readCount = stream.Read(buffer, 0, bufferSize);
                fs.Write(buffer, 0, readCount);
            } while (readCount > 0);
            stream.Close();
            fs.Close();
            webResponse.Close();

            bool bSuccess = fileInf.Exists && fileInf.Length > 0;
        }

        private void UnZip(string zipFile, string zipFolder)
        {
            ZipFile zip = ZipFile.Read(zipFile);
            zip.ExtractAll(zipFolder, ExtractExistingFileAction.OverwriteSilently);
            zip.Dispose();
            zip = null;
        }

        private void CopyFiles(string outFolder, string zipFolder)
        {
            foreach (string file in Directory.GetFiles(zipFolder))
            {
                try
                {
                    if (Path.GetFileName(file) == "Update.exe")
                    {
                        File.Copy(file, outFolder + "\\" + Path.GetFileName(file) + "-", true);
                    }
                    else
                    {
                        File.Copy(file, outFolder + "\\" + Path.GetFileName(file), true);
                    }
                }
                catch
                {

                }
            }
        }
    }
}
