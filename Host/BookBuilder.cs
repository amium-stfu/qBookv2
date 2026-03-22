using Microsoft.CodeAnalysis;
using Newtonsoft.Json;
using QB;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using static Community.CsharpSqlite.Sqlite3;


namespace Amium.Host
{

    public class PageDefinition
    {
        public string Name { get; set; }
        public string Text { get; set; }
        public int OrderIndex { get; set; }
        public bool Hidden { get; set; }
        public string Format { get; set; }
        public List<string> Includes { get; set; }
        public List<string> CodeOrder { get; set; }
        public string Section { get; set; }
        public string Url { get; set; }
    }

    public class qBookDefinition
    {
        public string ProjectName { get; set; } = "Unnamed";
        public string Version { get; set; } = "0.1.0";
        public string VersionHistory { get; set; } = "";
        public long VersionEpoch { get; set; } = 0;
        public bool StartFullScreen { get; set; } = false;
        public bool HidPageMenuBar { get; set; } = false;
        public string PasswordAdmin { get; set; } = null; //overrides the default Admin-Password
        public string PasswordService { get; set; } = null; //overrides the default Service-Password
        public string PasswordUser { get; set; } = null; //overrides the default User-Password
        public string Directory { get; set; } = null;
        public string Filename { get; set; } = null;
        public string SettingsDirectory { get; set; } = null;
        public string DataDirectory { get; set; } = null;
        public string TempDirectory { get; set; } = null;
        public string BackupDirectory { get; set; } = null;
        public string Language { get; set; } = null;
        public List<string> PageOrder { get; set; } = new List<string>();


    }

    internal static class BookBuilder
    {
        public static string SaveBook(string directory = null,string name = null)
        {
            if (!System.IO.Directory.Exists(directory) && directory != null)
            {
                System.IO.Directory.CreateDirectory(directory);
            }


            if (directory == null)
                directory = Core.ThisBook.DataDirectory;

            if(name == null)
                name = Core.ThisBook.ProjectName;

            string book = System.IO.Path.Combine(directory, name + ".code");

            if (!System.IO.Directory.Exists(book))
            {
                System.IO.Directory.CreateDirectory(book);
            }

          
            string bookJson = CreateBookJson(folder:book,name: name);
            string programCs = CreateProgramCs(System.IO.Path.Combine(book, "Program.cs"));
            string csproj = CreateCsproj("InMemoryProject", "net9.0-windows", System.IO.Path.Combine(book, name+ ".csproj"));
            string globalUsings = CreateGlobalUsing(System.IO.Path.Combine(book, "GlobalUsings.cs"));
            System.IO.Directory.CreateDirectory(System.IO.Path.Combine(book, "Pages"));
            SaveAllPages(book);

            string pagesInfo = string.Join("\n\n", qbook.Core.ActualMain.Objects.OfType<oPage>().Select(p => CreatePageDefinition(p)));
            System.IO.File.WriteAllText(System.IO.Path.Combine(directory, name + ".qbook"), pagesInfo);

            return bookJson + "\n\n" + programCs + "\n\n" + csproj;
        }



        #region Build
        private static string CreateProgramCs(string file = "T:\\qbooksave\\Program.cs")
        {
            List<oPage> pages = qbook.Core.ActualMain.Objects.OfType<oPage>().ToList();
            var sbProgram = new StringBuilder();
            sbProgram.AppendLine("namespace QB");
            sbProgram.AppendLine("{");
            sbProgram.AppendLine("\tpublic static class Program");
            sbProgram.AppendLine("\t{");
            foreach (oPage page in pages)
                sbProgram.AppendLine($"\t\tpublic static Definition{page.Name}.qPage {page.Name} {{ get; }} = new Definition{page.Name}.qPage();");

            sbProgram.AppendLine("\t\tpublic static void Initialize()");
            sbProgram.AppendLine("\t\t{");

            foreach (oPage page in pages)
                sbProgram.AppendLine($"\t\t\t{page.Name}.Initialize();");

            sbProgram.AppendLine("\t\t}");

            sbProgram.AppendLine("\t\tpublic static void Run()");
            sbProgram.AppendLine("\t\t{");
            foreach (oPage page in pages)
                sbProgram.AppendLine($"\t\t\t{page.Name}.Run();");
            sbProgram.AppendLine("\t\t}");

            sbProgram.AppendLine("\t\tpublic static void Destroy()");
            sbProgram.AppendLine("\t\t{");
            foreach (oPage page in pages)
                sbProgram.AppendLine($"\t\t\t{page.Name}.Destroy();");
            sbProgram.AppendLine("\t\t}");

            sbProgram.AppendLine("\t}");
            sbProgram.AppendLine("}");

            System.IO.File.WriteAllText(file, sbProgram.ToString());



            return sbProgram.ToString();
        }
        private static string CreateCsproj(string projectName = "InMemoryProject", string targetFramework = "net9.0-windows", string file = "T:\\qbooksave\\Program.csproj")
        {
            var sb = new StringBuilder();
            sb.AppendLine("<Project Sdk=\"Microsoft.NET.Sdk\">");
            sb.AppendLine("  <PropertyGroup>");
            sb.AppendLine($"    <OutputType>Library</OutputType>");
            sb.AppendLine($"    <TargetFramework>{targetFramework}</TargetFramework>");
            sb.AppendLine($"    <AssemblyName>{projectName}</AssemblyName>");
            sb.AppendLine("  </PropertyGroup>");

            bool needsSystemDrawingCommon = false;
            bool needsSystemIoPorts = false;
            sb.AppendLine("  <ItemGroup>");

            var refs = Core.Roslyn.GetProject?.MetadataReferences.OfType<PortableExecutableReference>() ?? Enumerable.Empty<PortableExecutableReference>();
            foreach (var r in refs)
            {
                if (string.IsNullOrWhiteSpace(r.FilePath))
                    continue;

                string fileName = System.IO.Path.GetFileNameWithoutExtension(r.FilePath);
                if (string.Equals(fileName, "System.Drawing", StringComparison.OrdinalIgnoreCase))
                {
                    needsSystemDrawingCommon = true;
                    continue; // nicht das alte Framework-Assembly einbetten
                }

                if (string.Equals(fileName, "System.IO.Ports", StringComparison.OrdinalIgnoreCase))
                {
                    needsSystemIoPorts = true;
                    continue; // nicht das alte Framework-Assembly einbetten
                }

                sb.AppendLine($"    <Reference Include=\"{fileName}\">");
                sb.AppendLine($"      <HintPath>{r.FilePath}</HintPath>");
                sb.AppendLine("    </Reference>");
            }

            sb.AppendLine("  </ItemGroup>");

            if (needsSystemDrawingCommon)
            {
                sb.AppendLine("  <ItemGroup>");
                sb.AppendLine("    <PackageReference Include=\"System.Drawing.Common\" Version=\"8.0.0\" />");
                sb.AppendLine("  </ItemGroup>");
            }

            if (needsSystemIoPorts)
            {
                sb.AppendLine("  <ItemGroup>");
                sb.AppendLine("    <PackageReference Include=\"System.IO.Ports\" Version=\"8.0.0\" />");
                sb.AppendLine("  </ItemGroup>");
            }


            sb.AppendLine("</Project>");
            System.IO.File.WriteAllText(file, sb.ToString());
            return sb.ToString();
        }

        private static string CreateGlobalUsing(string file = "T:\\qbooksave\\GlobalUsings.cs")
        {
            var sb = new StringBuilder();
            sb.AppendLine("global using static QB.Program;");
            System.IO.File.WriteAllText(file, sb.ToString());
            return sb.ToString();
        }

        private static string CreateBookJson(string folder = "T:\\qbooksave\\book.json", string name = "")
        {
           
           
            
            qBookDefinition def = new qBookDefinition
            {
                ProjectName = Core.ThisBook.ProjectName,
                Version = Core.ThisBook.Version,
                VersionHistory = Core.ThisBook.VersionHistory,
                VersionEpoch = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                StartFullScreen = Core.ThisBook.StartFullScreen,
                HidPageMenuBar = Core.ThisBook.HidPageMenuBar,
                PasswordAdmin = Core.ThisBook.PasswordAdmin,
                PasswordService = Core.ThisBook.PasswordService,
                PasswordUser = Core.ThisBook.PasswordUser,
                Directory = System.IO.Path.Combine(folder,name,".code"),
                Filename = name + ".qbook",
                SettingsDirectory = System.IO.Path.Combine(folder, name, ".settings"),
                DataDirectory = System.IO.Path.Combine(folder, name, ".data"),
                TempDirectory = System.IO.Path.Combine(folder, name, ".temp"),
                BackupDirectory = System.IO.Path.Combine(folder, name, ".backup"),
                Language = Core.ThisBook.Language,
                PageOrder = qbook.Core.ActualMain.Objects.OfType<oPage>().Select(p => p.Name).ToList()
            };
            string json = System.Text.Json.JsonSerializer.Serialize(def, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
            System.IO.File.WriteAllText(System.IO.Path.Combine(folder, "book.json"), json);
            return json;
        }

        private static string CreatePageDefinition(oPage page)
        {
            PageDefinition def = new PageDefinition
            {
                Name = page.Name,
                Text = page.Text,
                OrderIndex = page.OrderIndex,
                Hidden = page.Hidden,
                Format = page.Format == null ? "A4" : page.Format,
                Includes = page.Includes,
                CodeOrder = page.CodeOrder,
                Section = page.Section,
                Url = page.Url
            };
            string json = System.Text.Json.JsonSerializer.Serialize(def, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
            return json;
        }

        private static async Task SaveAllPages(string bookFolder)
        {
            foreach (oPage page in qbook.Core.ActualMain.Objects.OfType<oPage>())
            {
                string folder = System.IO.Path.Combine(bookFolder, "Pages",page.Name);
                if (!System.IO.Directory.Exists(folder))
                    System.IO.Directory.CreateDirectory(folder);

                string pageJson = CreatePageDefinition(page);
                string file = System.IO.Path.Combine(folder, "oPage.json");
                System.IO.File.WriteAllText(file, pageJson);

                string code = await Core.Roslyn.GetDocumentText(page.Name + ".qPage.cs");
                string codeFile = System.IO.Path.Combine(folder, page.Name + ".qPage.cs");
                System.IO.File.WriteAllText(codeFile, code);


                foreach (string codeName in page.CodeOrder)
                {
                    if (page.SubCodes != null)
                    {
                        code = await Core.Roslyn.GetDocumentText(codeName);
                        codeFile = System.IO.Path.Combine(folder, codeName);
                        System.IO.File.WriteAllText(codeFile, code);
                    }
                }

            }
        }

        private static string CreateDefaultNewBookPath()
        {
            var baseDirectory =
                qbook.Core.ThisBook?.Directory
                ?? Core.ThisBook?.Directory
                ?? Environment.CurrentDirectory;

            Directory.CreateDirectory(baseDirectory);

            const string baseName = "NewBook";
            var candidate = Path.Combine(baseDirectory, baseName + ".qbook");
            var suffix = 1;

            while (File.Exists(candidate) || Directory.Exists(candidate.Replace(".qbook", ".code")))
            {
                candidate = Path.Combine(baseDirectory, $"{baseName}{suffix}.qbook");
                suffix++;
            }

            return candidate;
        }

        public static async Task NewBook(string? uri = null)
        {
            uri ??= CreateDefaultNewBookPath();

            string name = Path.GetFileNameWithoutExtension(uri).Replace(".qbook", "");
            string directory = Path.GetDirectoryName(uri);
            string codeDir = uri.Replace(".qbook", ".code");
            if (!Directory.Exists(codeDir))
                Directory.CreateDirectory(codeDir);

            string link = "Created: " + System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            File.WriteAllText(Path.Combine(uri), link);

            string pageDir = Path.Combine(codeDir, "Pages");
            if (!Directory.Exists(pageDir))
                Directory.CreateDirectory(pageDir);

            string page1Dir = Path.Combine(pageDir, "Page1");
            if (!Directory.Exists(page1Dir))
                Directory.CreateDirectory(page1Dir);

            string opage = @"{
  ""Name"": ""Page1"",
  ""Text"": ""Page 1"",
  ""OrderIndex"": 0,
  ""Hidden"": false,
  ""Format"": ""A4"",
  ""Includes"": [],
  ""CodeOrder"": [
    ""Page1.qPage.cs""
  ],
  ""Section"": """",
  ""Url"": null
}";
            File.WriteAllText(Path.Combine(page1Dir, "oPage.json"), opage);

            string page1Code = @"namespace DefinitionPage1
{ //<CodeStart>
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using QB;

    public class qPage : BookPage
    {
        //common fields/properties/methods/classes/types go here

        public qPage() : base(""Page1"")
        {
        }

        protected override void OnInitialize()
        {
            //initialization code goes here

        }

        protected override void OnRun()
        {
            //run/work code goes here

        }

        protected override void OnDestroy()
        {
            //destroy/cleanup code goes here
        }
    }
    //<CodeEnd>
}";

            File.WriteAllText(Path.Combine(page1Dir, "Page1.qPage.cs"), page1Code);


            string bookJson = @$"{{
  ""ProjectName"": ""{name}"",
  ""Version"": ""0.1.0"",
  ""VersionHistory"": """",
  ""VersionEpoch"": 0,
  ""StartFullScreen"": false,
  ""HidPageMenuBar"": false,
  ""PasswordAdmin"": null,
  ""PasswordService"": null,
  ""PasswordUser"": null,
  ""Directory"": ""{directory.Replace("\\", "\\\\")}"",
  ""Filename"": ""{name}.qbook"",
  ""SettingsDirectory"": null,
  ""DataDirectory"": null,
  ""TempDirectory"":null,
  ""BackupDirectory"": null,
  ""Language"": ""en"",
  ""PageOrder"": [
    ""Page1""
  ]
}}";


            File.WriteAllText(Path.Combine(codeDir, "Book.json"), bookJson);

            File.WriteAllText(Path.Combine(codeDir, "GlobalUsing.cs"), "global using static QB.Program;");

            string programCode = @"namespace QB
{
	public static class Program
	{
		public static DefinitionPage1.qPage Page1 { get; } = new DefinitionPage1.qPage();
		public static void Initialize()
		{
			Page1.Initialize();
		}
		public static void Run()
		{
			Page1.Run();
		}
		public static void Destroy()
		{
			Page1.Destroy();
		}
	}
}";

            File.WriteAllText(Path.Combine(codeDir, "Program.cs"), programCode);



           
            BookBuilder.CreateCsproj(projectName: name, Path.Combine(codeDir, name + ".csproj"));
          

            await Core.OpenQbookAsync(uri);

        }

        #endregion





        #region Load

        internal static oPage oPageFromString(string json)
        {
            var data = JsonConvert.DeserializeObject(json, typeof(PageDefinition)) as PageDefinition;
            return new oPage
            {
                Name = data.Name,
                Text = data.Text,
                OrderIndex = data.OrderIndex,
                Hidden = data.Hidden,
                Format = data.Format,
                Includes = data.Includes ?? new List<string>(),
                CodeOrder = data.CodeOrder,
                Section = data.Section,
                Url = data.Url
            };
        }

        internal static async Task<Book> FromFolder(string folderPath, string bookname)
        {
            Debug.WriteLine("BookFromFolder: " + folderPath);
            Book newBook = new Book();
            newBook.Main = new oControl();

            Debug.WriteLine("Read Book.json");
            string bookJson = File.ReadAllText(Path.Combine(folderPath, "Book.json"));
            var qbook = JsonConvert.DeserializeObject(bookJson, typeof(qBookDefinition)) as qBookDefinition;

            newBook.Version = qbook.Version;
            newBook.ProjectName = qbook.ProjectName;
            newBook.VersionHistory = qbook.VersionHistory;
            newBook.VersionEpoch = qbook.VersionEpoch;
            newBook.StartFullScreen = qbook.StartFullScreen;
            newBook.HidPageMenuBar = qbook.HidPageMenuBar;
            newBook.PasswordAdmin = qbook.PasswordAdmin;
            newBook.PasswordService = qbook.PasswordService;
            newBook.PasswordUser = qbook.PasswordUser;
            newBook.Directory = qbook.Directory;
            newBook.Filename = qbook.Filename;
            newBook.Language = qbook.Language;
            newBook.PageOrder = qbook.PageOrder;
            newBook.SetDataDirectory(qbook.DataDirectory);
            newBook.SetSettingsDirectory(qbook.SettingsDirectory);
            newBook.SetTempDirectory(qbook.TempDirectory);


            List<string> reversePageOrder = newBook.PageOrder.AsEnumerable().Reverse().ToList();

            Debug.WriteLine("Processing Pages...");
            foreach (string page in reversePageOrder)
            {
                Debug.WriteLine(" - " + page);
            }

            List<oPage> pages = new List<oPage>();

            foreach (string page in reversePageOrder)
            {
                oPage opage = null;
                Debug.WriteLine(" -- " + page);

                Debug.WriteLine(" --- read page data");
                string pageFolder = Path.Combine(folderPath, "Pages", page);
                Debug.WriteLine(" --- page folder: " + pageFolder);
                string oPageJson = File.ReadAllText(Path.Combine(pageFolder, "oPage.json"));
                Debug.WriteLine(" --- deserialize page");
                opage = oPageFromString(oPageJson);
                Debug.WriteLine(" --- read page code");
                string filename = page + ".qPage.cs";
                Debug.WriteLine(" --- page code file: " + filename);
                opage.Filename = filename;
                Debug.WriteLine(" --- read code text");
                opage.Code = File.ReadAllText(Path.Combine(pageFolder, filename));
                Debug.WriteLine(" --- add page code to Roslyn");
                Core.Roslyn.AddCodeDocument(filename, opage.Code, true);
                Debug.WriteLine(" --- get page code document");
                opage.RoslynCodeDoc = Core.Roslyn.GetCodeDocument(filename);


                List<string> reverseCodeOrder = opage.CodeOrder.AsEnumerable().Reverse().ToList();

                foreach (string codeFile in reverseCodeOrder)
                {
                    if (codeFile.EndsWith("qPage.cs")) continue;

                    Debug.WriteLine(" --- " + codeFile);
                    string subCode = File.ReadAllText(Path.Combine(pageFolder, codeFile));

                    CodeDocument doc = new CodeDocument(codeFile, subCode, true, Core.Roslyn);
                    Core.Roslyn.AddCodeDocument(codeFile, subCode, true);

                    opage.SubCodeDocuments[codeFile] = doc;
                    //if (opage.Includes.Contains(codeFile))
                    //{
                    //    await opage.SubCodeDocuments[codeFile].Include();

                    //}
                }
                Debug.WriteLine(" ---- add page to book: " + opage.Name);
                pages.Add(opage);
            }
            pages.Reverse();

            foreach (oPage p in pages)
            {
                newBook.Main.Objects.Add(p);
            }

            Core.Roslyn.AddCodeDocument("Program.cs", File.ReadAllText(Path.Combine(folderPath, "Program.cs")), true);
            Core.Roslyn.AddCodeDocument("GlobalUsing.cs", "global using static QB.Program;", true);

            string name = newBook.Filename.Replace(".qbook", "");
            CreateCsproj("QbookStudioRoslyn", "net9.0-windows", System.IO.Path.Combine(folderPath, name + ".csproj"));

            QB.Root.ActiveQbook = newBook;
            return newBook;

        }


        #endregion

        #region XmlConvert

        internal static async Task XmlToFolder(string fullPath)
        {

            string root = fullPath.Replace(".qbook", ".code");
            if (!Directory.Exists(root)) Directory.CreateDirectory(root);

            string pageFolder = Path.Combine(root, "Pages");
            if (!Directory.Exists(pageFolder)) Directory.CreateDirectory(pageFolder);

            var roslynFiles = new List<(string fileName, string code)>();
            int pageCount = -1;
            string firstFile = null;


            List<string> Pages = new List<string>();

            int CodeIndex = 0;
            int PageIndex = 0;
            foreach (oPage page in qbook.Core.ActualMain.Objects.OfType<oPage>())
            {
                page.CodeOrder.Clear();
                page.Includes.Clear();

                string pageDir = Path.Combine(root, "Pages", page.Name);
                if (!Directory.Exists(pageDir)) Directory.CreateDirectory(pageDir);

                string className = "Definition" + page.Name + ".qPage";
                pageCount++;
                string code = page.CsCode;

                List<string> includes = CutInludesBlock(ref code);
                string pageCode = "namespace Definition" + page.Name + "{\r\n//<CodeStart>\r\n";
                pageCode += Regex.Replace(code, @"public class\s+@class_\w+", "public class qPage");
                pageCode += "\r\n//<CodeEnd>\r\n}";
                pageCode = ReplaceClassToDefinition(pageCode);
                string PageFileName = $"{page.Name}.qPage.cs";
                page.Filename = $"{page.Name}.qPage.cs";
                File.WriteAllText(Path.Combine(pageDir, PageFileName), pageCode);
                page.RoslynCodeDoc = Core.Roslyn.AddCodeDocument(PageFileName, pageCode, true);

                Core.ThisBook.PageOrder.Add(page.Name);
                page.CodeOrder.Add(PageFileName);

                page.OrderIndex = PageIndex;
                if (firstFile == null)
                    firstFile = PageFileName;

                var lines = code.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
                var usings = lines
                    .TakeWhile(l => !l.TrimStart().StartsWith("public class"))
                    .Where(l => l.TrimStart().StartsWith("using"))
                    .ToList();

                foreach (var subClass in page.CsCodeExtra)
                {
                    string subCode = "\r\n\r\nnamespace Definition" + page.Name
                        + "\r\n{\r\n//<CodeStart>\r\n"
                        + string.Join("\r\n", usings)
                        + subClass.Value
                        + "\r\n//<CodeEnd>\r\n"
                        + "\r\n}";

                    subCode = ReplaceClassToDefinition(subCode);
                    string subFileName = $"{page.Name}.{subClass.Key}.cs";
                    page.CodeOrder.Add(subFileName);
                    page.SubCodeDocuments[subFileName] = new CodeDocument(subFileName, subCode, true, Core.Roslyn);

                    Core.Roslyn.AddCodeDocument(subFileName,subCode,true);

                    File.WriteAllText(Path.Combine(pageDir, subFileName), subCode);
                    Debug.WriteLine("   wrote subcode file: " + subFileName);

                    //if (includes.Contains(subClass.Key))
                    //{
                    //    string file = page.Name + "." + subClass.Key + ".cs";
                    //    await page.SubCodeDocuments[file].Include();
                    //    page.SubCodeDocuments[file].UpdateCode();
                    //    page.Includes.Add(file);
                    //}
                }

                var dto = new PageDefinition
                {
                    Name = page.Name,
                    Text = page.Text,

                    OrderIndex = page.OrderIndex,
                    Hidden = page.Hidden,
                    Format = page.Format,
                    Includes = page.Includes,
                    Section = page.Section,
                    Url = page.Url,
                    CodeOrder = page.CodeOrder,

                };

                string oPageJson = JsonConvert.SerializeObject(dto, Newtonsoft.Json.Formatting.Indented);
                File.WriteAllText(Path.Combine(pageDir, "oPage.json"), oPageJson);

            }

            var sbProgram = new StringBuilder();
            sbProgram.AppendLine("namespace QB");
            sbProgram.AppendLine("{");
            sbProgram.AppendLine("\tpublic static class Program");
            sbProgram.AppendLine("\t{");
            foreach (oPage page in qbook.Core.ActualMain.Objects.OfType<oPage>())
                sbProgram.AppendLine($"\t\tpublic static Definition{page.Name}.qPage {page.Name} {{ get; }} = new Definition{page.Name}.qPage();");

            sbProgram.AppendLine("\t\tpublic static void Initialize()");
            sbProgram.AppendLine("\t\t{");

            foreach (oPage page in qbook.Core.ActualMain.Objects.OfType<oPage>())
                sbProgram.AppendLine($"\t\t\t{page.Name}.Initialize();");

            sbProgram.AppendLine("\t\t}");

            sbProgram.AppendLine("\t\tpublic static void Run()");
            sbProgram.AppendLine("\t\t{");
            foreach (oPage page in qbook.Core.ActualMain.Objects.OfType<oPage>())
                sbProgram.AppendLine($"\t\t\t{page.Name}.Run();");
            sbProgram.AppendLine("\t\t}");

            sbProgram.AppendLine("\t\tpublic static void Destroy()");
            sbProgram.AppendLine("\t\t{");
            foreach (oPage page in qbook.Core.ActualMain.Objects.OfType<oPage>())
                sbProgram.AppendLine($"\t\t\t{page.Name}.Destroy();");
            sbProgram.AppendLine("\t\t}");

            sbProgram.AppendLine("\t}");
            sbProgram.AppendLine("}");

            Core.Roslyn.AddCodeDocument("Program.cs", sbProgram.ToString(), true);
            Core.Roslyn.AddCodeDocument("GlobalUsing.cs", "global using static QB.Program;\r\n", true);

            File.WriteAllText(Path.Combine(root, "Program.cs"), sbProgram.ToString());
            File.WriteAllText(Path.Combine(root, "GlobalUsing.cs"), "global using static QB.Program;\r\n");

            string name = Path.GetFileNameWithoutExtension(fullPath).Replace(".qbook", "");

            BookBuilder.CreateCsproj(projectName: name, file: Path.Combine(root, name + ".csproj"));

            var project = new qBookDefinition
            {
                ProjectName = Core.ThisBook.Filename.Replace(".qbook", ""),
                Version = Core.ThisBook.Version,
                VersionHistory = Core.ThisBook.VersionHistory,
                VersionEpoch = Core.ThisBook.VersionEpoch,
                StartFullScreen = Core.ThisBook.StartFullScreen,
                HidPageMenuBar = Core.ThisBook.HidPageMenuBar,
                PasswordAdmin = Core.ThisBook.PasswordAdmin,
                PasswordService = Core.ThisBook.PasswordService,
                PasswordUser = Core.ThisBook.PasswordUser,
                Directory = Core.ThisBook.Directory,
                Filename = Core.ThisBook.Filename,
                SettingsDirectory = Core.ThisBook.SettingsDirectory,
                DataDirectory = Core.ThisBook.DataDirectory,
                BackupDirectory = Core.ThisBook.BackupDirectory,
                TempDirectory = Core.ThisBook.TempDirectory,
                Language = Core.ThisBook.Language,
                PageOrder = Core.ThisBook.PageOrder
            };

            string bookJson = JsonConvert.SerializeObject(project, Newtonsoft.Json.Formatting.Indented);
            File.WriteAllText(Path.Combine(root, "Book.json"), bookJson);



        }

        private static string ReplaceClassToDefinition(string code)
        {
            string result = code;


            foreach (oPage page in qbook.Core.ActualMain.Objects.OfType<oPage>())
            {
                string find = $"class_{page.Name}";
                string replace = $"Definition{page.Name}";
                Debug.WriteLine("find '" + find + "'");
                Debug.WriteLine("repl '" + replace + "'");

                string pattern = $@"\b{find}\b";

                result = Regex.Replace(result, pattern, replace);
            }

            return result;
        }

        private static List<string> CutInludesBlock(ref string source)
        {
            List<string> includes = new List<string>();
            if (string.IsNullOrWhiteSpace(source)) return includes;

            var regex = new Regex(@"//\+include\s+(\w+)", RegexOptions.Compiled);
            var lines = source.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            var newLines = new List<string>();
            var includeLines = new List<string>();
            int lineNumber = 0;
            int includeLineNumber = 0;

            bool inIncludeBlock = false;
            bool includeStartExists = false;
            bool includeEndExists = false;

            foreach (var line in lines)
            {
                var match = regex.Match(line);
                if (match.Success)
                {
                    includes.Add(match.Groups[1].Value);
                    includeLines.Add(line);
                }
                else
                {
                    if (!line.Contains("//<IncludeStart>") && !line.Contains("//<IncludeEnd>"))
                    {
                        newLines.Add(line);
                    }

                }

                if (line.Contains("public class @"))
                {
                    includeLineNumber = lineNumber;
                }
                //{
                //    // Stoppe das Sammeln, wenn ein Include-Block bereits existiert
                //    includeLines.Clear();
                //}
                lineNumber++;

            }
            //Debug.WriteLine("Insert Startline = " + includeLineNumber);
            //Debug.WriteLine("===== Includes ======");

            List<string> includeBlock = new List<string>();

            foreach (string l in includes) Debug.WriteLine(l);

            if (includeLines.Count > 0)
            {
                includeBlock.Add("//<IncludeStart>");
                includeBlock.AddRange(includeLines);
                includeBlock.Add("//<IncludeEnd>");

                // Optional: Du kannst entscheiden, wo der Block eingefügt wird.
                // Hier wird er am Anfang eingefügt.

            }
            else
            {
                includeBlock.Add("\t//<IncludeStart>");
                includeBlock.Add("");
                includeBlock.Add("\t//<IncludeEnd>");
            }

            //  newLines.InsertRange(includeLineNumber + 2, includeBlock);

            source = string.Join("\n", newLines);
            //Debug.WriteLine("===== Updated Source ======");
            //Debug.WriteLine(source);
            return includes;
        }

        #endregion



    }
}

