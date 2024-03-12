

using System.Diagnostics;

namespace CursorCompilerCLI {
    class Program {
        static void Main(string[] args) {
            string cursorFolderPath = args[0];
            string projectDestination = args[1];

            if(Path.GetExtension(projectDestination) != ".csproj") {
                Console.WriteLine("The destination project must be a .csproj file.");
                return;
            }

            string tempDirectory = SetUpTemporaryDirectory();
            IEnumerable<string> cursorFiles = Directory.EnumerateFiles(cursorFolderPath, "*.cur", SearchOption.TopDirectoryOnly);

            if(cursorFiles.Count() == 0) {
                Console.WriteLine("No cursor files found in chosen directory.");
                return;
            }

            GenerateRCFile(tempDirectory, cursorFiles);
            GenerateHeaderFile(tempDirectory, cursorFiles);
            GenerateCSFile(tempDirectory, cursorFiles);
            CompileResFile(tempDirectory);
            CopyToTargetProject(projectDestination, tempDirectory);

#if DEBUG
            Process.Start($"explorer", $"{tempDirectory}" );
            Console.WriteLine("Press any key to continue...");
            Console.ReadKey();
#endif
            Directory.Delete(tempDirectory, true);
        }

        private static string SetUpTemporaryDirectory() {
            string tempDirectory = Directory.CreateTempSubdirectory("CursorCompiler").FullName;

            FileStream source = File.OpenRead("Resource.rc");
            FileStream destination = File.Create(Path.Combine(tempDirectory, "Resource.rc"));

            source.CopyTo(destination);

            source.Close();
            destination.Close();
            source.Dispose();
            destination.Dispose();

            return tempDirectory;
        }

        private static void GenerateRCFile(string tempDirectory, IEnumerable<string> cursorFiles) {
            string fileContents = File.ReadAllText(Path.Combine(tempDirectory, "Resource.rc"));

            string result = "";
            int currentRCId = 1001;
            foreach (var file in cursorFiles) {
                result += $"IDC_CUR{currentRCId} CURSOR \"{file.Replace("\\", "\\\\")}\"\n";
                currentRCId++;
            }

            fileContents = fileContents.Replace("<<REPLACE>>", result);
            File.WriteAllText(Path.Combine(tempDirectory, "Resource.rc"), fileContents);
        }
        private static void GenerateHeaderFile(string tempDirectory, IEnumerable<string> cursorFiles) {
            File.Create(Path.Combine(tempDirectory, "Resource.h")).Close();
            
            string result = "";
            int currentHeaderId = 1001;
            foreach (var file in cursorFiles) {
                result += $"#define IDC_CUR{currentHeaderId} {currentHeaderId}\n";
                currentHeaderId++;
            }

            File.WriteAllText(Path.Combine(tempDirectory, "Resource.h"), result);
        }
        private static void GenerateCSFile(string tempDirectory, IEnumerable<string> cursorFiles) {
            File.Create(Path.Combine(tempDirectory, "ImportedCursors.cs")).Close();
            int currentHeaderId = 1001;
            string cursorDefinitionText = "";
            cursorDefinitionText += "using System;\n";
            cursorDefinitionText += "namespace CursorCompiler {\n";
            cursorDefinitionText += "public static class ImportedCursors {\n";
            foreach (var file in cursorFiles) {
                string displayName = Path.GetFileNameWithoutExtension(file).Replace(' ', '_').Replace('-','_');
                cursorDefinitionText += "public const int " + displayName + " = " + currentHeaderId + ";\n";
                currentHeaderId++;
            }
            cursorDefinitionText += "}\n";
            cursorDefinitionText += "}";
            File.WriteAllText(Path.Combine(tempDirectory, "ImportedCursors.cs"), cursorDefinitionText);
        }

        private static void CompileResFile(string tempDirectory) {
            Process p = new Process() {
                StartInfo = new ProcessStartInfo() { 
                    FileName = "rc.exe", 
                    Arguments = $"{Path.Combine(tempDirectory, "Resource.rc")}" 
                } 
            };

            p.Start();

            while(!p.HasExited) {
                Thread.Sleep(100);
            }
        }


        private static void CopyToTargetProject(string projectDestination, string tempDirectory) {
            string projectRoot = Path.GetDirectoryName(projectDestination);
            FileStream resSource = File.OpenRead(Path.Combine(tempDirectory, "Resource.res"));
            FileStream resDestination = File.Create(Path.Combine(projectRoot, "Resource.res"));
        
            resSource.CopyTo(resDestination);

            resSource.Close();
            resDestination.Close();

            FileStream csSource = File.OpenRead(Path.Combine(tempDirectory, "ImportedCursors.cs"));
            FileStream csDestination = File.Create(Path.Combine(projectRoot, "ImportedCursors.cs"));

            csSource.CopyTo(csDestination);

            csSource.Close();
            csDestination.Close();
        }


    }
}