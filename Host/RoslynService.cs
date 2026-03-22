
using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Classification;
using Microsoft.CodeAnalysis.Completion;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Emit;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Host;
using Microsoft.CodeAnalysis.Host.Mef;
using Microsoft.CodeAnalysis.MSBuild;
using Microsoft.CodeAnalysis.Rename;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using static Amium.Host.RoslynService;
using AccessibilityCode = Microsoft.CodeAnalysis.Accessibility;
using RoslynDocument = Microsoft.CodeAnalysis.Document;
using System.Data;

namespace Amium.Host
{
    public class CodeDocument
    {

        public string Filename { get; }
        public string Code { get; set; }
        public RoslynDocument? Document => Core.Roslyn.GetDocumentByFilename(Filename);

        public DocumentId? Id => Document?.Id;

        private readonly RoslynService _roslyn;

        public bool Active
        {
            get;
            set;
        }

        public CodeDocument(string filename, string code, bool active, RoslynService roslyn)
        {
            Filename = filename;
            Code = code;
            _roslyn = roslyn;
            Active = active;
        }

        public async Task<string> GetTextAsync()
        {
            if (Document != null)
            {
                var code = await Document.GetTextAsync();
                return code.ToString();
            }
            return Code;
        }
        public void UpdateCode()
        {
            var newText = SourceText.From(Code, Encoding.UTF8);
            if (Document != null)
            {
                var updatedDoc = Document.WithText(newText);
                _roslyn.GetWorkspace.TryApplyChanges(updatedDoc.Project.Solution);

            }
        }

        public void Exclude()
        {
            if (Document != null)
            {
                _roslyn.ExcludeDocument(Document.Id);
            }
            Active = false;
        }

        public async Task Include()
        {
            if (Document == null)
            {
                await _roslyn.IncludeDocument(Filename, Code);
            }
            Active = true;
        }
    }



    public sealed partial class RoslynService
    {

        DataTable? Dummy = new DataTable();
        private MSBuildWorkspace? _ws;
        private Project? _project;
        private readonly SemaphoreSlim _loadSemaphore = new(1, 1);
        private bool _isLoading;

        public Project GetProject => _project;

        private readonly object _buildLock = new();
        private bool _isBuildingAssembly = false;
        public Project? GetCurrentProject() => _adhocWs?.CurrentSolution.GetProject(_projectId);
        public ProjectId? GetCurrentProjectId() => _projectId;

        private bool _useInMemory = true;
        private AdhocWorkspace? _adhocWs;

        private readonly Dictionary<string, CodeDocument> _docMap = new();

        private string? _assemblyOutputPath;
        private string? _pdbOutputPath;
        private int _artifactGeneration;
        private object? _runtimeAssemblyLoadContext;
        private WeakReference? _runtimeAssemblyLoadContextWeakReference;

        // NEW: cache a single MEF host to avoid repeatedly allocating composition containers
        private static readonly HostServices s_host = CreateMefHost();

        // NEW: cache metadata references by path so we don’t re-open PE files repeatedly
        private static readonly object s_refLock = new();
        private static volatile List<MetadataReference>? s_cachedReferences;

        private static bool LooksLikeBuildOutputPath(string path)
        {
            return path.IndexOf("\\bin\\", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   path.IndexOf("\\obj\\", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string? ResolveByFileNameFromCodeDir(string codeDir, string fileName)
        {
            if (string.IsNullOrWhiteSpace(codeDir) || string.IsNullOrWhiteSpace(fileName) || !Directory.Exists(codeDir))
            {
                return null;
            }

            var normalizedFileName = Path.GetFileName(fileName);
            if (string.IsNullOrWhiteSpace(normalizedFileName))
            {
                return null;
            }

            var matches = Directory.GetFiles(codeDir, normalizedFileName, SearchOption.AllDirectories);
            if (matches.Length == 0)
            {
                return null;
            }

            var pagesMatch = matches.FirstOrDefault(path =>
                path.IndexOf("\\Pages\\", StringComparison.OrdinalIgnoreCase) >= 0);
            return !string.IsNullOrWhiteSpace(pagesMatch) ? pagesMatch : matches[0];
        }

        private string GetCodeDirectoryPath()
        {
            if (!string.IsNullOrWhiteSpace(_assemblyOutputPath))
            {
                var dir = Path.GetDirectoryName(_assemblyOutputPath);
                if (!string.IsNullOrWhiteSpace(dir))
                {
                    return dir;
                }
            }

            var book = Core.ThisBook;
            if (book != null)
            {
                return Path.Combine(book.Directory, book.ProjectName + ".Code");
            }

            return string.Empty;
        }


        private Assembly LoadRuntimeAssembly(byte[] peImage, byte[] pdbImage)
        {
            try
            {
                var alcType = Type.GetType("System.Runtime.Loader.AssemblyLoadContext, System.Runtime.Loader", throwOnError: false);
                if (alcType == null)
                {
                    return Assembly.Load(peImage, pdbImage);
                }

                if (_runtimeAssemblyLoadContext != null)
                {
                    try
                    {
                        alcType.GetMethod("Unload", BindingFlags.Instance | BindingFlags.Public)
                            ?.Invoke(_runtimeAssemblyLoadContext, null);
                        _runtimeAssemblyLoadContext = null;

                        if (_runtimeAssemblyLoadContextWeakReference != null)
                        {
                            for (int i = 0; _runtimeAssemblyLoadContextWeakReference.IsAlive && i < 6; i++)
                            {
                                GC.Collect();
                                GC.WaitForPendingFinalizers();
                                GC.Collect();
                            }

                            if (_runtimeAssemblyLoadContextWeakReference.IsAlive)
                            {
                               Debug.WriteLine("[Roslyn] Previous runtime load context is still alive (assembly references may still exist).");
                            }
                        }
                    }
                    catch (Exception unloadEx)
                    {
                       Debug.WriteLine("[Roslyn] Unloading previous runtime load context failed: " + unloadEx.Message);
                    }
                }

                var ctor = alcType.GetConstructor(new[] { typeof(string), typeof(bool) });
                if (ctor == null)
                {
                    return Assembly.Load(peImage, pdbImage);
                }

                var context = ctor.Invoke(new object[] { $"qbook-roslyn-runtime-{DateTime.UtcNow:yyyyMMddHHmmssfff}", true });
                if (context == null)
                {
                    return Assembly.Load(peImage, pdbImage);
                }

                var loadFromStreamMethod = alcType.GetMethod(
                    "LoadFromStream",
                    BindingFlags.Instance | BindingFlags.Public,
                    binder: null,
                    types: new[] { typeof(Stream), typeof(Stream) },
                    modifiers: null
                );

                if (loadFromStreamMethod == null)
                {
                    return Assembly.Load(peImage, pdbImage);
                }

                using var pe = new MemoryStream(peImage, writable: false);
                using var pdb = new MemoryStream(pdbImage, writable: false);

                var loaded = loadFromStreamMethod.Invoke(context, new object[] { pe, pdb }) as Assembly;
                if (loaded == null)
                {
                    return Assembly.Load(peImage, pdbImage);
                }

                _runtimeAssemblyLoadContext = context;
                _runtimeAssemblyLoadContextWeakReference = new WeakReference(context, trackResurrection: false);
                Debug.WriteLine($"[Roslyn] Runtime assembly loaded in collectible runtime context. MVID={loaded.ManifestModule.ModuleVersionId}");
                return loaded;
            }
            catch (Exception ex)
            {
               Debug.WriteLine("[Roslyn] Collectible runtime load unavailable, fallback to Assembly.Load: " + ex.Message);
                return Assembly.Load(peImage, pdbImage);
            }
        }


        public bool IsProjectLoaded => _project != null && ((_useInMemory && _adhocWs != null) || (!_useInMemory && _ws != null));
        public async Task LoadProjectAsync(string csprojPath)
        {
            Debug.WriteLine("LoadProjectAsync: '" + csprojPath + "'");

            if (string.IsNullOrWhiteSpace(csprojPath) || !File.Exists(csprojPath)) return;
            await _loadSemaphore.WaitAsync();
            try
            {
                if (_project != null || _isLoading) return;
                _isLoading = true;
                if (!MSBuildLocator.IsRegistered)
                {
                    try
                    {
                        var instances = MSBuildLocator.QueryVisualStudioInstances().ToArray();
                        if
                            (instances.Length > 0) MSBuildLocator.RegisterInstance(instances.OrderByDescending(i => i.Version).First());
                        else
                            MSBuildLocator.RegisterDefaults();
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine("MSBuildLocator registration failed: " + ex.Message);
                       Debug.WriteLine("[RoslynServices]: LoadProjectAsync MSBuildLocator registration failed: " + ex.Message);
                    }
                }
                var props = new Dictionary<string, string> { { "DesignTimeBuild", "true" }, { "BuildingInsideVisualStudio", "true" } };



                _ws = MSBuildWorkspace.Create(props);

                _ws.WorkspaceFailed += (s, e) =>Debug.WriteLine($"[WorkspaceFailed] {e.Diagnostic.Kind}: {e.Diagnostic.Message}");
                try
                {
                    _project = await _ws.OpenProjectAsync(csprojPath);
                }
                catch (Exception ex)
                {
                   Debug.WriteLine("OpenProjectAsync failed: " + ex.Message);
                    _project = null;
                }
            }
            finally
            {
                _isLoading = false;
                _loadSemaphore.Release();
            }
        }

        private EventHandler<WorkspaceDiagnosticEventArgs>? _workspaceFailedHandler;


        public void ResetDocumentsOnly()
        {
            if (_adhocWs == null || _projectId == null)
                return;

            var solution = _adhocWs.CurrentSolution;
            var project = solution.GetProject(_projectId);
            if (project == null)
                return;

            foreach (var doc in project.Documents.ToList())
            {
                solution = solution.RemoveDocument(doc.Id);
            }

            _adhocWs.TryApplyChanges(solution);
            _docMap.Clear();
        }

        //public void ResetDocumentsOnly()
        //{
        //    if (_adhocWs == null)
        //        _adhocWs = new AdhocWorkspace(s_host);

        //    try
        //    {
        //        // Projekt entfernen, statt ClearSolution()
        //        if (_projectId != null)
        //        {
        //            var newSolution = _adhocWs.CurrentSolution.RemoveProject(_projectId);
        //            _adhocWs.TryApplyChanges(newSolution);
        //        }

        //        // Neues Projekt mit gecachten Referenzen
        //        var projectInfo = ProjectInfo.Create(
        //            _projectId ?? ProjectId.CreateNewId(),
        //            VersionStamp.Create(),
        //            "InMemoryProject",
        //            "InMemoryAssembly",
        //            LanguageNames.CSharp,
        //            metadataReferences: _referenceCache ?? GetOrBuildDefaultReferences(),
        //            parseOptions: new CSharpParseOptions(LanguageVersion.Preview),
        //            compilationOptions: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
        //        );

        //        _adhocWs.AddProject(projectInfo);
        //        _projectId = projectInfo.Id;
        //        _project = _adhocWs.CurrentSolution.GetProject(_projectId);
        //        _docMap.Clear();
        //    }
        //    catch (Exception ex)
        //    {
        //       Debug.WriteLine("[Roslyn] ResetDocumentsOnly failed: " + ex.Message);
        //    }
        //}

        public async Task ReloadDocumentsAsync(IEnumerable<(string fileName, string code)> files)
        {
            ResetDocumentsOnly();

            foreach (var (fileName, code) in files)
            {
                AddCodeDocument(fileName, code, true);
            }
        }





        public void Reset()
        {

            if (_ws != null && _workspaceFailedHandler != null)
                _ws.WorkspaceFailed -= _workspaceFailedHandler;

            _ws?.Dispose();
            _adhocWs?.Dispose();
            _ws = null;
            _adhocWs = null;
            _project = null;
            _docMap.Clear();
            _useInMemory = true;
            _projectId = null;

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            _adhocWs = new AdhocWorkspace(s_host);
        }



        public class ReferenceCacheFile
        {
            public List<string> References { get; set; } = new();
            public string AppVersion { get; set; } = "1.0";
        }



        private string ReferenceCachePath =>
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "RoslynReferenceCache.json");

        private bool TryLoadReferenceCache(out List<MetadataReference> refs)
        {
           
           
            refs = new();
            return false;

            try
            {
                if (!File.Exists(ReferenceCachePath))
                    return false;

                var json = File.ReadAllText(ReferenceCachePath);
                var data = JsonSerializer.Deserialize<ReferenceCacheFile>(json);
                if (data == null || data.References.Count == 0)
                    return false;

                foreach (var path in data.References)
                {
                    if (!File.Exists(path))
                        return false; // Cache ungültig → neu erzeugen

                    refs.Add(MetadataReference.CreateFromFile(path));
                }

                return true;
            }
            catch
            {
                return false;
            }
        }
        private void SaveReferenceCache(IEnumerable<MetadataReference> refs)
        {
            try
            {
                var file = new ReferenceCacheFile();

                foreach (var r in refs.OfType<PortableExecutableReference>())
                {
                    if (!string.IsNullOrWhiteSpace(r.FilePath))
                        file.References.Add(r.FilePath);
                }

                var json = JsonSerializer.Serialize(file, new JsonSerializerOptions
                {
                    WriteIndented = true
                });

                File.WriteAllText(ReferenceCachePath, json);
            }
            catch
            {
                // Ignorieren – Cache ist optional
            }
        }

        private static List<MetadataReference> AddLoadedAssembliesAsReferences()
        {
            var refs = new List<MetadataReference>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    string? location = asm.Location;
                    if (string.IsNullOrEmpty(location)) continue; // Dynamische oder Ref-Assemblies ohne Pfad ignorieren
                    if (!seen.Add(location)) continue; // Duplikate vermeiden

                    refs.Add(MetadataReference.CreateFromFile(location));
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[Roslyn] Skip assembly {asm.FullName}: {ex.Message}");
                }
            }

            return refs;
        }
        
        private void EnsureOutputPaths(string sender, string? assemblyName, bool force = false)
        {
            if (Core.ThisBook == null) return;
            if (!force && !string.IsNullOrWhiteSpace(_assemblyOutputPath) && !string.IsNullOrWhiteSpace(_pdbOutputPath))
                return;

            var effectiveName = string.IsNullOrWhiteSpace(assemblyName) ? "InMemoryAssembly" : assemblyName;
            var outputDirectory = Path.Combine(Path.GetTempPath(), "qbookRoslyn");

            try
            {
                Directory.CreateDirectory(outputDirectory);
                string suffix = string.Empty;

                if (force)
                {
                    int generation = Interlocked.Increment(ref _artifactGeneration);
                    suffix = $"_{generation:00000}";
                }
                else
                {
                    _artifactGeneration = 0;
                }

                //_assemblyOutputPath = Path.Combine(outputDirectory, $"{effectiveName}{suffix}.dll");
                //_pdbOutputPath = Path.Combine(outputDirectory, $"{effectiveName}{suffix}.pdb");
                
                _assemblyOutputPath = Path.Combine(Core.ThisBook.DataDirectory.Replace(".data",".code"), "Book.dll");
                _pdbOutputPath = Path.Combine(Core.ThisBook.DataDirectory.Replace(".data", ".code"), "Book.pdb");

                Debug.WriteLine("");
            }
            catch (Exception ex)
            {
               Debug.WriteLine("[Roslyn] EnsureOutputPaths failed: " + ex.Message);
                _assemblyOutputPath = null;
                _pdbOutputPath = null;
            }
        }

        public void ClearBuildArtifacts()
        {
            void TryDelete(ref string? path)
            {
                if (string.IsNullOrWhiteSpace(path))
                    return;

                try
                {
                    if (File.Exists(path))
                    {
                        File.Delete(path);
                    }
                }
                catch (Exception ex)
                {
                   Debug.WriteLine($"[Roslyn] ClearBuildArtifacts failed for '{path}': {ex.Message}");
                }
                finally
                {
                    path = null;
                }
            }

            TryDelete(ref _assemblyOutputPath);
            TryDelete(ref _pdbOutputPath);
        }

        public void CreateProject()
        {
            if (_adhocWs == null || _adhocWs.Services.Kind != WorkspaceKind.Host)
            {
                _adhocWs?.Dispose();
                _adhocWs = new AdhocWorkspace(s_host);
            }
            else
            {
                try { _adhocWs.ClearSolution(); } catch { }
            }

            var projectId = ProjectId.CreateNewId();
            _projectId = projectId;

            List<MetadataReference> references;

            Debug.WriteLine("[Roslyn] Building references...");

            references = new List<MetadataReference>();

            // deine bisherigen Logiken (Basisreferenzen, AppDomain, netstandard usw.)
            references.Add(MetadataReference.CreateFromFile(typeof(object).Assembly.Location));
            references.Add(MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location));
            references.Add(MetadataReference.CreateFromFile(typeof(System.Drawing.Point).Assembly.Location));
            references.Add(MetadataReference.CreateFromFile(typeof(Microsoft.CSharp.RuntimeBinder.Binder).Assembly.Location));
            references.Add(MetadataReference.CreateFromFile(typeof(System.Text.Json.Serialization.JsonAttribute).Assembly.Location));
            references.AddRange(AddLoadedAssembliesAsReferences());

           // EnsureOutputPaths(sender: "CreateProject", assemblyName: "InMemoryAssembly");
            var parseOptions = new CSharpParseOptions(LanguageVersion.Preview);
            var compilationOptions = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
                .WithOptimizationLevel(OptimizationLevel.Debug);

            // ✅ Projekt erstellen
            var projectInfo = ProjectInfo.Create(
                projectId,
                VersionStamp.Create(),
                "InMemoryProject",
                "InMemoryAssembly",
                LanguageNames.CSharp,
                metadataReferences: references,
                parseOptions: parseOptions,
                compilationOptions: compilationOptions
            );

            _adhocWs.AddProject(projectInfo);
            _project = _adhocWs.CurrentSolution.GetProject(projectId);
        }

        private List<MetadataReference> _referenceCache = new();



        public AdhocWorkspace GetWorkspace => _adhocWs;
        public ProjectId GetProjectId => _projectId;

        ProjectId _projectId;

       

        public async Task<string?> GetDocumentTextAsync(string fileName)
        {
            var doc = GetDocumentByFilename(fileName);
            if (doc == null) return null;

            var text = await doc.GetTextAsync();
            return text.ToString();
        }

        public CodeDocument AddCodeDocument(string filename, string code, bool active)
        {
            if (_adhocWs == null || _projectId == null)
                throw new InvalidOperationException("Workspace/Project not initialized.");


            var docInfo = DocumentInfo.Create(
                DocumentId.CreateNewId(_projectId),
                name: filename,
                loader: TextLoader.From(TextAndVersion.Create(SourceText.From(code, Encoding.UTF8), VersionStamp.Create())),
                filePath: FilePath(filename));

            var doc = _adhocWs.AddDocument(docInfo);

            Debug.WriteLine($"[AddCodeDocument] Added document '{doc.Name}' with ID {doc.Id} to project '{_project?.Name}' Filepath :{doc.FilePath}");
         

            _project = _adhocWs.CurrentSolution.GetProject(_projectId)!;


            lock (_docMap)
            {
                _docMap[filename] = new CodeDocument(filename, code, active, this);
            }

            return _docMap[filename];
        }
        string FilePath(string filename)
        {
            string[] data = filename.Split('.');

            if (data.Count() > 2)
                return Path.Combine(Core.OpenedDirectory, Path.GetFileName(Core.OpenedDirectory) + ".code", "Pages", data[0], filename);

            return Path.Combine(Core.OpenedDirectory, Path.GetFileName(Core.OpenedDirectory) + ".code", filename);
        }

     

        public void RemoveCodeDocument(string filename)
        {
            if (_adhocWs == null || _projectId == null)
                throw new InvalidOperationException("Workspace/Project not initialized.");
            var doc = _adhocWs.CurrentSolution.Projects
                .SelectMany(p => p.Documents)
                .FirstOrDefault(d => d.Name == filename);
            if (doc == null) return;
            var newSolution = _adhocWs.CurrentSolution.RemoveDocument(doc.Id);
            if (_adhocWs.TryApplyChanges(newSolution))
            {
                _project = _adhocWs.CurrentSolution.GetProject(_projectId);
            }
            lock (_docMap)
            {
                _docMap.Remove(filename);
            }
        }

        internal void ExcludeDocument(DocumentId id)
        {
            if (_adhocWs == null || _projectId == null)
                throw new InvalidOperationException("Workspace/Project not initialized.");

            var doc = _adhocWs.CurrentSolution.GetDocument(id);
            if (doc == null) return;

            var filename = doc.Name;
            var newSolution = _adhocWs.CurrentSolution.RemoveDocument(id);
            if (_adhocWs.TryApplyChanges(newSolution))
            {
                _project = _adhocWs.CurrentSolution.GetProject(_projectId);
            }

            lock (_docMap)
            {
                _docMap.Remove(filename);
            }
        }
        public CodeDocument GetCodeDocument(string fileName)
        {
            lock (_docMap)
            {
              foreach ( var doc in _docMap)
                {
                    Debug.WriteLine($"[GetCodeDocument] '{doc.Key}' vs '{fileName}'");  
                }
                
                _docMap.TryGetValue(fileName, out var codeDoc);
                return codeDoc;
            }
        }
        public async Task IncludeDocument(string fileName, string code)
        {
            return; // Nicht mehr benötigt, da AddCodeDocument jetzt immer mit FilePath arbeitet
            var projectId = _project.Id;

            // Prüfen, ob das Dokument schon existiert
            var existingDoc = _project.Documents.FirstOrDefault(d => d.Name == fileName);
            if (existingDoc != null)
                return; // Schon vorhanden

            var source = SourceText.From(code, Encoding.UTF8);
            var added = _adhocWs.AddDocument(projectId, fileName, source);
            var withPath = added.WithFilePath(FilePath(fileName));
            _adhocWs.TryApplyChanges(withPath.Project.Solution);
            _project = _adhocWs.CurrentSolution.GetProject(projectId);
            var compilation = await _project.GetCompilationAsync();
        }
        public async Task <string> GetDocumentText(string fileName)
        {
            string text = $"Not found {fileName}";
            var doc = _project.Documents.FirstOrDefault(d => d.Name == fileName);
            if (doc != null)
            {
                var sourceText = await doc.GetTextAsync();
                text = sourceText.ToString();
            }

            return text;
        }

        private static HostServices CreateMefHost()
        {
            var assemblies = MefHostServices.DefaultAssemblies
                .Concat(new[]
                {
                    typeof(CompletionService).Assembly,
                    typeof(CSharpCompilation).Assembly,
                    typeof(Microsoft.CodeAnalysis.CSharp.Formatting.CSharpFormattingOptions).Assembly,
                })
                .Distinct();
            return MefHostServices.Create(assemblies);
        }

        private static AdhocWorkspace CreateAdhocWorkspace()
        {
            var assemblies = MefHostServices.DefaultAssemblies
                .Concat(new[]
                {
            typeof(CompletionService).Assembly, // Microsoft.CodeAnalysis.Features
            typeof(CSharpCompilation).Assembly, // Microsoft.CodeAnalysis.CSharp
            typeof(Microsoft.CodeAnalysis.CSharp.Formatting.CSharpFormattingOptions).Assembly, // CSharp.Workspaces
                })
                .Distinct();
            var host = MefHostServices.Create(assemblies);
            return new AdhocWorkspace(host);
        }

        public RoslynDocument? GetDocumentByFilename(string fileName)
        {
            return _adhocWs.CurrentSolution.Projects
                .SelectMany(p => p.Documents)
                .FirstOrDefault(d => d.Name == fileName);
        }

        public async Task UpdateOpenDocumentAsync(RoslynDocument doc, string text)
        {
            if (doc == null) return;

            var sourceText = SourceText.From(text, Encoding.UTF8);
            var updatedDoc = doc.WithText(sourceText);

            _adhocWs.TryApplyChanges(updatedDoc.Project.Solution);
        }

        public async Task<(Microsoft.CodeAnalysis.Completion.CompletionItem[] items, int spanStart)> GetCompletionsAsync(RoslynDocument doc, int caretPosition)
        {
            if (doc == null)
                return (new Microsoft.CodeAnalysis.Completion.CompletionItem[0], caretPosition);

            var completionService = CompletionService.GetService(doc);
            if (completionService == null)
                return (new Microsoft.CodeAnalysis.Completion.CompletionItem[0], caretPosition);

            var completions = await completionService.GetCompletionsAsync(doc, caretPosition);
            if (completions == null)
                return (new Microsoft.CodeAnalysis.Completion.CompletionItem[0], caretPosition);

            var items = completions.Items.ToArray();
            var spanStart = completions.Span.Start;

            return (items, spanStart);
        }
        public async Task<(RoslynDocument Document, int Line, int Column)?> GoToDefinitionAsync(RoslynDocument doc, int caret)
        {
            if (doc == null) return null;

            var semanticModel = await doc.GetSemanticModelAsync();
            var syntaxTree = await doc.GetSyntaxTreeAsync();
            if (semanticModel == null || syntaxTree == null) return null;

            var root = await syntaxTree.GetRootAsync();
            var position = caret > 0 ? caret - 1 : caret;
            var token = root.FindToken(position);
            var node = token.Parent;
            if (node == null) return null;

            var symbol = semanticModel.GetSymbolInfo(node).Symbol ?? semanticModel.GetDeclaredSymbol(node);

            Debug.WriteLine($"symbol: '{symbol}'");

            if (symbol == null) return null;

            var definition = await SymbolFinder.FindSourceDefinitionAsync(symbol, doc.Project.Solution) ?? symbol;
            var location = definition.Locations.FirstOrDefault(loc => loc.IsInSource);
            if (location == null) return null;

            var linePosition = location.GetLineSpan().StartLinePosition;
            var document = doc.Project.Solution.GetDocument(location.SourceTree);
            if (document == null) return null;

            Debug.WriteLine($"found in: '{document.Name}'");
            return (document, linePosition.Line, linePosition.Character);
        }
        public async Task<Dictionary<string, string>?> RenameSymbolAsync(RoslynDocument doc, int caret, string newName)
        {
            if (doc == null || string.IsNullOrWhiteSpace(newName)) return null;
            var semantic = await doc.GetSemanticModelAsync();
            var tree = await doc.GetSyntaxTreeAsync();
            if (semantic == null || tree == null) return null;
            var root = await tree.GetRootAsync();
            var token = root.FindToken(Math.Max(0, caret - 1));
            var symbol = semantic.GetSymbolInfo(token.Parent!).Symbol ?? semantic.GetDeclaredSymbol(token.Parent!);
            if (symbol == null) return null;
            var solution = doc.Project.Solution;
            var newSolution = await Renamer.RenameSymbolAsync(solution, symbol, newName, solution.Workspace.Options);
            if (!solution.Workspace.TryApplyChanges(newSolution)) return null;
            // Optional: Änderungen sammeln wie bisher
            return new Dictionary<string, string>();
        }
        public async Task<IReadOnlyList<string>> LookupInstanceMemberNamesAsync(RoslynDocument document, string text, int position)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            if (_adhocWs == null || _project == null) return names.ToList();
            await UpdateOpenDocumentAsync(document, text);
            var doc = document;
            if (doc == null) return names.ToList();
            var semantic = await doc.GetSemanticModelAsync();
            var root = await doc.GetSyntaxRootAsync();
            if (semantic == null || root == null) return names.ToList();
            var token = root.FindToken(Math.Min(position, root.FullSpan.End - 1));
            var node = token.Parent;
            INamedTypeSymbol? typeSym = null;
            while (node != null && typeSym == null)
            {
                if (node is TypeDeclarationSyntax tds) typeSym = semantic.GetDeclaredSymbol(tds) as INamedTypeSymbol;
                node = node.Parent;
            }
            if (typeSym == null) return names.ToList();
            foreach (var sym in semantic.LookupSymbols(position).Where(s => !s.IsStatic && s.ContainingType != null))
            {
                var ct = sym.ContainingType;
                bool inHierarchy = ct != null && (SymbolEqualityComparer.Default.Equals(ct, typeSym) || IsBaseOf(typeSym, ct));
                if (!inHierarchy) continue;
                switch (sym)
                {
                    case IMethodSymbol ms when ms.MethodKind == MethodKind.Ordinary: names.Add(ms.Name); break;
                    case IPropertySymbol ps: names.Add(ps.Name); break;
                    case IFieldSymbol fs when !fs.IsImplicitlyDeclared && !fs.Name.StartsWith("<"): names.Add(fs.Name); break;
                    case IEventSymbol es: names.Add(es.Name); break;
                }
            }
            return names.ToList();
            static bool IsBaseOf(INamedTypeSymbol derived, INamedTypeSymbol candidate)
            {
                var b = derived.BaseType;
                while (b != null)
                {
                    if (SymbolEqualityComparer.Default.Equals(b, candidate)) return true;
                    b = b.BaseType;
                }
                return false;
            }
        }
        public async Task<IReadOnlyList<string>> EnumerateHierarchyInstanceMembersAsync(RoslynDocument document, string text, int position)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            if (_adhocWs == null || _project == null) return names.ToList();
            await UpdateOpenDocumentAsync(document, text);
            var doc = document;
            if (doc == null) return names.ToList();
            var semantic = await doc.GetSemanticModelAsync();
            var root = await doc.GetSyntaxRootAsync();
            if (semantic == null || root == null) return names.ToList();
            var token = root.FindToken(Math.Min(position, root.FullSpan.End - 1));
            var node = token.Parent;
            INamedTypeSymbol? typeSym = null;
            while (node != null && typeSym == null)
            {
                if (node is TypeDeclarationSyntax tds)
                    typeSym = semantic.GetDeclaredSymbol(tds) as INamedTypeSymbol;
                node = node?.Parent;
            }
            if (typeSym == null) return names.ToList();
            void Add(INamedTypeSymbol t)
            {
                foreach (var member in t.GetMembers())
                {
                    if (member.IsImplicitlyDeclared) continue;
                    if (member.IsStatic) continue;
                    if (member.DeclaredAccessibility == AccessibilityCode.Private && !SymbolEqualityComparer.Default.Equals(t, typeSym)) continue;
                    switch (member)
                    {
                        case IPropertySymbol p: names.Add(p.Name); break;
                        case IFieldSymbol f when !f.Name.StartsWith("<"): names.Add(f.Name); break;
                        case IMethodSymbol m when m.MethodKind == MethodKind.Ordinary: names.Add(m.Name); break;
                        case IEventSymbol e: names.Add(e.Name); break;
                    }
                }
            }
            for (var t = typeSym; t != null && t.SpecialType != SpecialType.System_Object; t = t.BaseType)
                Add(t);
            return names.ToList();
        }
        public async Task<IReadOnlyList<string>> FindTypeCandidatesAsync(RoslynDocument document, string text, string prefix)
        {
            var list = new HashSet<string>(StringComparer.Ordinal);
            if (_adhocWs == null || _project == null || string.IsNullOrWhiteSpace(prefix)) return list.ToList();
            await UpdateOpenDocumentAsync(document, text);
            var doc = document;
            if (doc == null) return list.ToList();
            var root = await doc.GetSyntaxRootAsync();
            var semantic = await doc.GetSemanticModelAsync();
            if (root == null || semantic == null) return list.ToList();

            string currentNs = "";
            var nsDecl = root.DescendantNodes().OfType<FileScopedNamespaceDeclarationSyntax>().FirstOrDefault();
            if (nsDecl != null)
                currentNs = nsDecl.Name.ToString();
            else
            {
                var blockNs = root.DescendantNodes().OfType<NamespaceDeclarationSyntax>().FirstOrDefault();
                if (blockNs != null) currentNs = blockNs.Name.ToString();
            }

            var fileUsings = root.DescendantNodes().OfType<UsingDirectiveSyntax>()
                                  .Select(u => u.Name.ToString())
                                  .ToHashSet(StringComparer.Ordinal);

            var compilation = semantic.Compilation;
            void VisitNamespace(INamespaceSymbol ns)
            {
                foreach (var member in ns.GetMembers())
                {
                    if (member is INamespaceSymbol childNs)
                    {
                        VisitNamespace(childNs);
                    }
                    else if (member is INamedTypeSymbol typeSym)
                    {
                        if (typeSym.Name.Length == 0) continue;
                        if (!typeSym.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) continue;
                        if (typeSym.TypeKind == TypeKind.Error) continue;
                        if (typeSym.DeclaredAccessibility is not (AccessibilityCode.Public or AccessibilityCode.Internal)) continue;
                        if (typeSym.DeclaredAccessibility == AccessibilityCode.Internal && !SymbolEqualityComparer.Default.Equals(typeSym.ContainingAssembly, compilation.Assembly))
                            continue;
                        var typeNs = typeSym.ContainingNamespace?.ToDisplayString() ?? string.Empty;
                        string shortName = typeSym.Name;
                        string fullyQualified = typeSym.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat).Replace("global::", string.Empty);
                        if (string.Equals(typeNs, currentNs, StringComparison.Ordinal) || string.IsNullOrEmpty(typeNs))
                        {
                            list.Add(shortName);
                        }
                        else
                        {
                            bool imported = fileUsings.Contains(typeNs);
                            if (imported) list.Add(shortName); // allow short name because namespace imported
                            string minimal = typeNs + "." + shortName;
                            list.Add(minimal);
                            list.Add(fullyQualified);
                        }
                    }
                }
            }
            VisitNamespace(compilation.GlobalNamespace);
            return list.ToList();
        }
        public async Task<string?> AddUsingIfUniqueTypeAsync(RoslynDocument document, string text, string typeName)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(typeName) || _adhocWs == null || _project == null) return null;
                await UpdateOpenDocumentAsync(document, text);
                var doc = document;
                if (doc == null) return null;
                var semantic = await doc.GetSemanticModelAsync();
                var root = await doc.GetSyntaxRootAsync() as CSharpSyntaxNode;
                if (semantic == null || root == null) return null;
                // Already resolvable?
                var existing = semantic.LookupNamespacesAndTypes(root.FullSpan.Start, name: typeName);
                if (existing.Any(sym => sym is INamedTypeSymbol)) return null; // already available
                // Collect candidate types (public/internal) by traversing namespaces
                var compilation = semantic.Compilation;
                var candidates = new List<INamedTypeSymbol>();
                void VisitNamespace(INamespaceSymbol ns)
                {
                    foreach (var m in ns.GetMembers())
                    {
                        if (m is INamespaceSymbol child) VisitNamespace(child);
                        else if (m is INamedTypeSymbol t && t.Name.Equals(typeName, StringComparison.Ordinal))
                        {
                            if (t.DeclaredAccessibility is AccessibilityCode.Public or AccessibilityCode.Internal)
                            {
                                if (t.DeclaredAccessibility == AccessibilityCode.Internal && !SymbolEqualityComparer.Default.Equals(t.ContainingAssembly, compilation.Assembly)) continue;
                                candidates.Add(t);
                            }
                        }
                    }
                }
                VisitNamespace(compilation.GlobalNamespace);
                if (candidates.Count != 1) return null; // only auto if unique
                var target = candidates[0];
                var nsName = target.ContainingNamespace?.ToDisplayString() ?? string.Empty;
                if (string.IsNullOrEmpty(nsName)) return null;
                // Already has using?
                if (root.DescendantNodes().OfType<UsingDirectiveSyntax>().Any(u => u.Name.ToString() == nsName)) return null;
                // Insert using (keep at top, after existing usings, before namespace/type decl)
                var usingDirective = SyntaxFactory.UsingDirective(SyntaxFactory.ParseName(nsName)).WithTrailingTrivia(SyntaxFactory.CarriageReturnLineFeed);
                var firstNonUsing = root.ChildNodes().FirstOrDefault(n => n is not UsingDirectiveSyntax && n.Kind() != SyntaxKind.ShebangDirectiveTrivia);
                var usings = root.DescendantNodes().OfType<UsingDirectiveSyntax>().ToList();
                SyntaxNode newRoot;
                if (usings.Count > 0)
                {
                    var lastUsing = usings.Last();
                    newRoot = root.InsertNodesAfter(lastUsing, new[] { usingDirective });
                }
                else if (firstNonUsing != null)
                {
                    newRoot = root.InsertNodesBefore(firstNonUsing, new[] { usingDirective });
                }
                else
                {
                    // root has no usings; prepend at top
                    newRoot = root.WithLeadingTrivia(usingDirective.GetLeadingTrivia())
                                   .InsertNodesBefore(root.ChildNodes().FirstOrDefault()!, new[] { usingDirective });
                }
                var newText = newRoot.NormalizeWhitespace().ToFullString();
                return newText;
            }
            catch (Exception ex)
            {
                Debug.WriteLine("AddUsingIfUniqueTypeAsync error: " + ex.Message);
                return null;
            }
        }
        public static async Task<string?> FormatCSharpAsync(string source, bool useTabs, int indentSize, CancellationToken ct = default)
        {
            try
            {
                // Workspace mit Features-Host (siehe CreateAdhocWorkspace in RoslynServices)
                using var workspace = CreateAdhocWorkspace();


                // Optionen (ähnlich VS/VSCode)
                var options = workspace.Options
                .WithChangedOption(FormattingOptions.UseTabs, LanguageNames.CSharp, useTabs)
                .WithChangedOption(FormattingOptions.IndentationSize, LanguageNames.CSharp, indentSize)
                .WithChangedOption(FormattingOptions.TabSize, LanguageNames.CSharp, indentSize);


                // Parse- und Compilation-Optionen
                var parse = new CSharpParseOptions(LanguageVersion.Preview);
                var comp = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary);


                // Adhoc-Projekt + Dokument aufbauen
                var projId = ProjectId.CreateNewId();
                var docId = DocumentId.CreateNewId(projId);


                var solution = workspace.CurrentSolution
                .AddProject(projId, "AdhocProject", "AdhocProject", LanguageNames.CSharp)
                .WithProjectParseOptions(projId, parse)
                .WithProjectCompilationOptions(projId, comp)
                .AddDocument(docId, "Temp.cs", SourceText.From(source, Encoding.UTF8));


                if (!workspace.TryApplyChanges(solution))
                    return null;


                var document = workspace.CurrentSolution.GetDocument(docId)!;


                // Formatieren (reicht für Whitespaces/Einrückungen/Braces etc.)
                var formattedDoc = await Formatter.FormatAsync(document, options, ct).ConfigureAwait(false);
                var formattedText = await formattedDoc.GetTextAsync(ct).ConfigureAwait(false);
                return formattedText.ToString();
            }
            catch
            {
                return null; // Fallback zulassen
            }
        }

        public List<RoslynDocument> GetAllDocuments()
        {
            return _adhocWs?.CurrentSolution.Projects
                .SelectMany(p => p.Documents)
                .ToList() ?? new List<RoslynDocument>();
        }


        public List<string> ErrorFiles = new List<string>();

        Stopwatch buildWatch = new Stopwatch();

        public int BuildDuration = 0;
        public string BuildResult = "";
        public bool BuildSuccess = false;

        private bool _enablePdbSourcePathNormalization = false;

        // optional: öffentlich schaltbar (z.B. über Settings/UI/Command)
        public bool EnablePdbSourcePathNormalization
        {
            get => _enablePdbSourcePathNormalization;
            set => _enablePdbSourcePathNormalization = value;
        }


        public async Task<Assembly?> BuildAssemblyAsync()
        {
            Debug.WriteLine("=== Build Assembly");
            Debug.WriteLine("[Diag] Id  =" + _project?.Id);
            Debug.WriteLine("[Diag] Docs=" + _project?.Documents.Count());
            Debug.WriteLine("[Diag] Has Program.cs=" + (_project?.Documents.Any(d => d.Name == "Program.cs")));

           

            lock (_buildLock)
            {
                if (_isBuildingAssembly) return null;
                _isBuildingAssembly = true;
            }
            try
            {
                if (_projectId == null || _adhocWs == null)
                {
                   Debug.WriteLine("[Roslyn] BuildAssemblyAsync: no projectId/workspace.");
                    BuildSuccess = false;
                    return null;
                }

                // Hole immer gezielt das aktuelle Projekt über _projectId (nicht FirstOrDefault)
                var project = _adhocWs.CurrentSolution.GetProject(_projectId);
                if (project == null)
                {
                   Debug.WriteLine("[Roslyn] BuildAssemblyAsync: projectId not found in CurrentSolution.");
                    BuildSuccess = false;
                    return null;
                }

                // Dokumentpfade unmittelbar vor dem Emit normalisieren, damit Portable PDB korrekte Source-Pfade enthält
                Debug.WriteLine($"\n━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
                Debug.WriteLine($"[BuildAssemblyAsync] Starting path normalization for {project.Documents.Count()} documents");
                Debug.WriteLine($"━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n");

                var normalizedSolution = _adhocWs.CurrentSolution;
                var normalizedAny = false;
                int docIndex = 0;
                foreach (var document in project.Documents)
                {
                    docIndex++;
                    Debug.WriteLine($"\n[Doc {docIndex}/{project.Documents.Count()}] Processing: '{document.Name}' FilePath: {document.FilePath}");
                }

                // Merke aktuelle Projekt-ID/Doc-Anzahl zur Diagnose
                Debug.WriteLine($"[Build] Using ProjectId={project.Id} Docs={project.Documents.Count()}");

                var compilation = await project.GetCompilationAsync();

            
                compilation = compilation.WithOptions(
                    compilation.Options.WithOptimizationLevel(OptimizationLevel.Debug));

                using var peStream = new MemoryStream();
                using var pdbStream = new MemoryStream();

                var emitResult = compilation.Emit(
                    peStream,
                    pdbStream,
                    options: new EmitOptions(debugInformationFormat: DebugInformationFormat.PortablePdb));

                if (!emitResult.Success)
                {
                    var diags = emitResult.Diagnostics
                        .Where(d => d.Severity == DiagnosticSeverity.Error)
                        .ToList();

                    ErrorFiles = diags
                        .Select(d => d.Location.GetMappedLineSpan().Path)
                        .Where(p => !string.IsNullOrEmpty(p))
                        .Distinct()
                        .ToList();

                    string log =

                    "[Roslyn] Emit failed:\n" +
                        string.Join("\n", diags.Select(d =>
                        {
                            var span = d.Location.GetMappedLineSpan();
                            return $"{Path.GetFileName(span.Path)}({span.StartLinePosition.Line + 1},{span.StartLinePosition.Character + 1}): {d.Id}: {d.GetMessage()}";
                        }));

                   Debug.WriteLine(log);
                    Debug.WriteLine(log);

                    BuildSuccess = false;
                    return null;
                }

                peStream.Position = 0;
                pdbStream.Position = 0;
              

                var peImage = peStream.ToArray();
                var pdbImage = pdbStream.ToArray();


                var dllPath = Path.Combine(Core.ThisBook.CodeDirectory, "Book.dll");
                var pdbPath = Path.Combine(Core.ThisBook.CodeDirectory, "Book.pdb");

                File.WriteAllBytes(dllPath, peImage);
                File.WriteAllBytes(pdbPath, pdbImage);

                Debug.WriteLine($"[Roslyn] Wrote DLL: {dllPath}");
                Debug.WriteLine($"[Roslyn] Wrote PDB: {pdbPath}");



                if (!string.IsNullOrWhiteSpace(_assemblyOutputPath))
                {
                    try
                    {
                        File.WriteAllBytes(_assemblyOutputPath, peImage);
                    }
                    catch (Exception ex)
                    {
                       Debug.WriteLine($"[Roslyn] Writing assembly failed: {ex.Message}");
                    }
                }

                if (!string.IsNullOrWhiteSpace(_pdbOutputPath))
                {
                    try
                    {
                        File.WriteAllBytes(_pdbOutputPath, pdbImage);
                    }
                    catch (Exception ex)
                    {
                       Debug.WriteLine($"[Roslyn] Writing PDB failed: {ex.Message}");
                    }
                }


                var asm = LoadRuntimeAssembly(peImage, pdbImage);
                string codeDir = Path.Combine(Core.ThisBook.Directory, Core.ThisBook.ProjectName + ".Code");

                var compilationSourcePaths = compilation.SyntaxTrees
                    .Select(tree => tree.FilePath)
                    .Where(path => !string.IsNullOrWhiteSpace(path))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
                var sourcePathSample = string.Join(" | ", compilationSourcePaths.Take(10));
                var hasCodeDirSourcePath = compilationSourcePaths.Any(path =>
                    path.StartsWith(codeDir, StringComparison.OrdinalIgnoreCase));
                var hornetNestViewPath = compilationSourcePaths.FirstOrDefault(path =>
                    path.EndsWith("\\HornetNest.view.cs", StringComparison.OrdinalIgnoreCase));
                var protagonistsQPagePath = compilationSourcePaths.FirstOrDefault(path =>
                    path.EndsWith("\\Protagonists.qPage.cs", StringComparison.OrdinalIgnoreCase));
                var containsHornetNestView = !string.IsNullOrWhiteSpace(hornetNestViewPath);
                var containsProtagonistsQPage = !string.IsNullOrWhiteSpace(protagonistsQPagePath);

                Dictionary<string, string> logData = new Dictionary<string, string>
                {

                    { "BuidDate", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") },
                    { "DocumentCount", project.Documents.Count().ToString() },
                    { "EmitSuccess", emitResult.Success.ToString() },
                    { "ErrorCount", emitResult.Diagnostics.Count(d => d.Severity == DiagnosticSeverity.Error).ToString() },
                    { "AssemblySizeKB", (peImage.Length / 1024.0).ToString("F2") },
                    { "dllPath", _assemblyOutputPath },
                    { "PdbPath", _pdbOutputPath },
                    { "PdbLength", (pdbImage.Length / 1024.0).ToString("F2") },

                    // WICHTIG für VS Code / dein Plugin:
                    { "HostProcessId", Process.GetCurrentProcess().Id.ToString() },
                    { "HostProcessName", Process.GetCurrentProcess().ProcessName },
                    { "AssemblyName", project.AssemblyName ?? "InMemoryAssembly" },
                    { "ModuleMvid", asm.ManifestModule.ModuleVersionId.ToString() },
                    { "AssemblyLocation", asm.Location ?? string.Empty },
                    { "SourceFileCount", compilationSourcePaths.Count.ToString() },
                    { "SourceRoot", codeDir },
                    { "SourceRootMatch", hasCodeDirSourcePath.ToString() },
                    { "SourceFileSample", sourcePathSample },
                    { "ContainsHornetNestView", containsHornetNestView.ToString() },
                    { "HornetNestViewPath", hornetNestViewPath ?? string.Empty },
                    { "ContainsProtagonistsQPage", containsProtagonistsQPage.ToString() },
                    { "ProtagonistsQPagePath", protagonistsQPagePath ?? string.Empty }
                };

                JsonSerializerOptions jsonOptions = new JsonSerializerOptions
                {
                    WriteIndented = true
                };

                string jsonLog = JsonSerializer.Serialize(logData, jsonOptions);

                Directory.CreateDirectory(codeDir);

                string assemblyLogFile = Path.Combine(codeDir, "AssemblyLog.json");
                File.WriteAllText(assemblyLogFile, jsonLog);

                // VS Code launch.json im selben Verzeichnis erzeugen/aktualisieren
                var symbolSearchPaths = new List<string>();
                if (!string.IsNullOrWhiteSpace(_assemblyOutputPath))
                {
                    var asmDir = Path.GetDirectoryName(_assemblyOutputPath);
                    if (!string.IsNullOrWhiteSpace(asmDir))
                        symbolSearchPaths.Add(asmDir);
                }

                if (!string.IsNullOrWhiteSpace(_pdbOutputPath))
                {
                    var pdbDir = Path.GetDirectoryName(_pdbOutputPath);
                    if (!string.IsNullOrWhiteSpace(pdbDir))
                        symbolSearchPaths.Add(pdbDir);
                }

                symbolSearchPaths.Add(codeDir);
                var distinctSymbolPaths = symbolSearchPaths
                    .Where(p => !string.IsNullOrWhiteSpace(p))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray();

           

                var launchData = new Dictionary<string, object>
                {
                    { "version", "0.2.0" },
                    { "configurations", new object[]
                        {
                            new Dictionary<string, object>
                            {
                                { "name", "qbook: Attach to host process" },
                                { "type", "clr" },
                                { "request", "attach" },
                                { "processId", Process.GetCurrentProcess().Id },
                                { "justMyCode", false },
                                { "requireExactSource", false },
                                { "suppressJITOptimizations", true },
                                { "logging", new Dictionary<string, object>
                                    {
                                        { "moduleLoad", true },
                                        { "exceptions", true },
                                        { "programOutput", true }
                                    }
                                },
                                
                                { "symbolOptions", new Dictionary<string, object>
                                    {
                                        { "searchPaths", distinctSymbolPaths },
                                        { "searchMicrosoftSymbolServer", false }
                                    }
                                }
                            }
                        }
                    }
                };

                string launchJson = JsonSerializer.Serialize(launchData, new JsonSerializerOptions
                {
                    WriteIndented = true
                });

                var vscodeDir = Path.Combine(codeDir, ".vscode");
                Directory.CreateDirectory(vscodeDir);
                string launchFile = Path.Combine(vscodeDir, "launch.json");
                File.WriteAllText(launchFile, launchJson);


                BuildSuccess = true;

                // SHA256-Hashes berechnen
                string peSha256Hash = "";
                string pdbSha256Hash = "";
                string firstSourceFile = "<unknown>";

                try
                {
                    using (var sha256 = SHA256.Create())
                    {
                        peSha256Hash = BitConverter.ToString(sha256.ComputeHash(peImage)).Replace("-", "");
                        pdbSha256Hash = BitConverter.ToString(sha256.ComputeHash(pdbImage)).Replace("-", "");
                    }
                }
                catch (Exception ex)
                {
                   Debug.WriteLine($"[Roslyn] SHA256 calculation failed: {ex.Message}");
                }

                // Erste Quelldatei aus PDB extrahieren
                try
                {
                    using (var pdbStreamReader = new MemoryStream(pdbImage))
                    using (var metadataReaderProvider = MetadataReaderProvider.FromPortablePdbStream(pdbStreamReader))
                    {
                        var metadataReader = metadataReaderProvider.GetMetadataReader();
                        var documents = metadataReader.Documents;

                        if (documents.Count > 0)
                        {
                            var firstDoc = metadataReader.GetDocument(documents.First());
                            firstSourceFile = metadataReader.GetString(firstDoc.Name);
                        }
                    }
                }
                catch (Exception ex)
                {
                   Debug.WriteLine($"[Roslyn] PDB source file extraction failed: {ex.Message}");
                }

                Core.SendToEditor("LogInfo", "[HOST] <<<<<< Building Assembly >>>>>>");
                Core.SendToEditor("LogInfo", "[HOST] Project ID             " + Core.Roslyn.GetProjectId);
                Core.SendToEditor("LogInfo", "[HOST] Assembly Name          " + asm.FullName);
                Core.SendToEditor("LogInfo", "[HOST] LoadedAssemblyMvid     " + asm.ManifestModule.ModuleVersionId);
                Core.SendToEditor("LogInfo", "[HOST] LoadedAssemblyLocation " + (string.IsNullOrEmpty(asm.Location) ? "<In-Memory>" : asm.Location));
                Core.SendToEditor("LogInfo", "[HOST] LoadedTimestamp        " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"));
                Core.SendToEditor("LogInfo", "[HOST] PE-SHA256              " + peSha256Hash);
                Core.SendToEditor("LogInfo", "[HOST] PDB-SHA256             " + pdbSha256Hash);
                Core.SendToEditor("LogInfo", "[HOST] FirstSourceFile        " + firstSourceFile);
                Core.SendToEditor("LogInfo", "[HOST] ContainsHornetNestView " + containsHornetNestView);
                Core.SendToEditor("LogInfo", "[HOST] HornetNestViewPath     " + (hornetNestViewPath ?? "<missing>"));
                Core.SendToEditor("LogInfo", "[HOST] ContainsProtagonistsQP " + containsProtagonistsQPage);
                Core.SendToEditor("LogInfo", "[HOST] ProtagonistsQPagePath  " + (protagonistsQPagePath ?? "<missing>"));



                return asm;
            }
            finally
            {

                lock (_buildLock) _isBuildingAssembly = false;
            }
        }
        private static bool IsManagedAssembly(string path)
        {
            try
            {
                using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read))
                using (var br = new BinaryReader(fs))
                {
                    if (fs.Length < 0x3C + 4) return false;
                    fs.Position = 0x3C;
                    int peHeaderOffset = br.ReadInt32();
                    if (peHeaderOffset + 0x18 + 2 > fs.Length) return false;
                    fs.Position = peHeaderOffset + 0x18;
                    ushort magic = br.ReadUInt16();
                    long pos = (magic == 0x010b) ? peHeaderOffset + 0xF8 : peHeaderOffset + 0x108;
                    if (pos + 0x70 + 8 > fs.Length) return false;
                    fs.Position = pos + 0x70;
                    uint cliHeaderRva = br.ReadUInt32();
                    uint cliHeaderSize = br.ReadUInt32();
                    return cliHeaderRva != 0 && cliHeaderSize != 0;
                }
            }
            catch
            {
                return false;
            }
        }


        #region Auto-Complete
        public async Task<IReadOnlyList<string>> GetAutoCompleteSuggestionsAsync(RoslynDocument document, string text, int caretPosition, char? triggerChar = null)
        {
            if (document == null) return Array.Empty<string>();

            // Dokument aktualisieren
            var updatedDoc = document.WithText(SourceText.From(text, Encoding.UTF8));

            // Completions abrufen
            var (items, _) = await GetCompletionsAsync(updatedDoc, caretPosition);
            if (items == null || items.Length == 0) return Array.Empty<string>();

            // Nur DisplayText zurückgeben, sortiert und ohne Duplikate
            return items
                .Select(i => i.DisplayText)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(s => s)
                .ToList();
        }

        public async Task<IReadOnlyList<string>> GetAutoCompleteSuggestionsAsync(
            RoslynDocument document,
            string text,
            int caretPosition,
            string prefix)
        {
            if (document == null) return Array.Empty<string>();

            var updatedDoc = document.WithText(SourceText.From(text, Encoding.UTF8));
            var (items, _) = await GetCompletionsAsync(updatedDoc, caretPosition);

            if (items == null || items.Length == 0) return Array.Empty<string>();

            return items
                .Select(i => i.DisplayText)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Where(s => string.IsNullOrEmpty(prefix) || s.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                .OrderBy(s => s)
                .ToList();
        }

        #endregion

        #region Signature Help

        public class SignatureParameter
        {
            public string Name { get; set; }
            public string Type { get; set; }
            public string DefaultValue { get; set; }
            public string CurrentValue { get; set; }
        }

        private async Task<IReadOnlyList<SignatureParameter>> ExtractParametersAsync(ArgumentListSyntax argumentList, RoslynDocument doc, SyntaxNode node)
        {
            var model = await doc.GetSemanticModelAsync();
            if (model == null) return Array.Empty<SignatureParameter>();

            IMethodSymbol? methodSymbol = null;

            if (node is InvocationExpressionSyntax inv)
            {
                var info = model.GetSymbolInfo(inv);
                methodSymbol = info.Symbol as IMethodSymbol ?? info.CandidateSymbols.OfType<IMethodSymbol>().FirstOrDefault();
            }
            else if (node is ObjectCreationExpressionSyntax obj)
            {
                var type = model.GetTypeInfo(obj).Type as INamedTypeSymbol ??
                           model.GetSymbolInfo(obj.Type).Symbol as INamedTypeSymbol;
                methodSymbol = type?.InstanceConstructors.FirstOrDefault();
            }

            if (methodSymbol == null) return Array.Empty<SignatureParameter>();

            var usedParams = argumentList.Arguments
                .Select(arg => arg.NameColon?.Name.Identifier.ValueText)
                .Where(name => !string.IsNullOrEmpty(name))
                .ToHashSet();

            var list = new List<SignatureParameter>();
            foreach (var param in methodSymbol.Parameters)
            {
                if (usedParams.Contains(param.Name)) continue;

                string defaultValue = param.HasExplicitDefaultValue ? param.ExplicitDefaultValue?.ToString() ?? "null" : "????";
                string type = param.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
                string currentValue = argumentList.Arguments.FirstOrDefault(a => a.NameColon?.Name.Identifier.ValueText == param.Name)?.Expression.ToString() ?? defaultValue;

                list.Add(new SignatureParameter
                {
                    Name = param.Name,
                    Type = type,
                    DefaultValue = defaultValue,
                    CurrentValue = currentValue
                });
            }

            return list;
        }


        #endregion







    }

    public static class SpanMapper
    {
        /// <summary>
        /// Überträgt Roslyn-Spans in Scintilla-Markierungen – offsetbasiert und tab-korrigiert.
        /// </summary>
        public static Task ApplyBucketsAsync(Scintilla editor, int roslynLength, Dictionary<int, List<TextSpan>> buckets)
        {
            string editorText = editor.Text;
            int tabWidth = editor.TabWidth;

            foreach (var kvp in buckets)
            {
                editor.IndicatorCurrent = kvp.Key;
                editor.IndicatorClearRange(0, editor.TextLength); // optional: nur wenn nötig

                foreach (var span in kvp.Value)
                {
                    int start = Clamp(span.Start, 0, editor.TextLength);
                    int length = Clamp(span.Length, 0, editor.TextLength - start);
                    editor.IndicatorFillRange(start, length);
                }
            }

            return Task.CompletedTask;
        }

        public static int Clamp(int value, int min, int max)
        {
            return (value < min) ? min : (value > max) ? max : value;
        }



        /// <summary>
        /// Übersetzt Roslyn-Offset in visuelle Scintilla-Position unter Berücksichtigung von Tabs.
        /// </summary>
        private static int RoslynOffsetToScintillaPosition(string text, int offset, int tabWidth)
        {
            int position = 0;
            for (int i = 0; i < offset && i < text.Length; i++)
            {
                if (text[i] == '\t')
                {
                    int spacesToNextTabStop = tabWidth - (position % tabWidth);
                    position += spacesToNextTabStop;
                }
                else
                {
                    position++;
                }
            }
            return position;
        }
    }
}
