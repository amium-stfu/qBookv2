
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

using System.Xml;
using System.Xml.Serialization;
using RoslynDocument = Microsoft.CodeAnalysis.Document;


namespace Amium.Host
{
    [Serializable]
    public class Book
    {
        public string View = "";
        public string Version = "0.1.0";
        public string VersionHistory = "";
        public long VersionEpoch = 0;

        //2025-09-06 STFU
        public bool StartFullScreen = false;
        public bool HidPageMenuBar = false;
        public List<string> PageOrder = new List<string>();

        [XmlIgnore]
        public RoslynDocument Program;
        [XmlIgnore]
        public RoslynDocument Global;

        public string ProjectName { get; set; }
        public string PasswordAdmin { get; set; } = null; //overrides the default Admin-Password
        public string PasswordService { get; set; } = null; //overrides the default Service-Password
        public string PasswordUser { get; set; } = null; //overrides the default User-Password

        public string _Directory;
        public string Directory
        {
            get
            {
                return _Directory;
            }
            set
            {
                var changed = _Directory != value;
                _Directory = value;
                if (changed)
                {
                    //OnStaticPropertyChangedEvent("Directory", _Directory);
                }
            }
        }

 

        public string _Filename;
        [XmlIgnore]
        public string Filename
        {
            get
            {
                return _Filename;
            }
            set
            {
                var changed = _Filename != value;
                _Filename = value;
                if (changed)
                {
                    //OnStaticPropertyChangedEvent("Filename", _Filename);
                }
            }
        }

        public string CodeDirectory => Path.Combine(Directory, ProjectName + ".code");

        public void SetDataDirectory(string dir) { _DataDirectory = dir; }
        public void SetSettingsDirectory(string dir) { _SettingsDirectory = dir; }
        public void SetTempDirectory(string dir) { _TempDirectory = dir; }


        static Regex NameVersionExtRegex = new Regex(@"(?<name>.*?)(\.v(?<version>\d.*))?(?<ext>\.aBook)");
        string _DataDirectory = null;
        public string DataDirectory
        {
            get
            {
                if (_DataDirectory == null)
                {
                    //create/use default data-directory
                    string qbookName = Core.ThisBook.Filename;
                    Match m = NameVersionExtRegex.Match(qbookName);
                    if (m.Success)
                    {
                        string name = m.Groups["name"].Value;
                        string version = m.Groups["version"].Value; 
                        string ext = m.Groups["ext"].Value.ToLower();
                        if (ext == ".aBook")
                        {
                            string dir = Path.Combine(Core.ThisBook.Directory, name + ".data");
                            if (!System.IO.Directory.Exists(dir))
                                System.IO.Directory.CreateDirectory(dir);

                            _DataDirectory = dir;
                        }
                    }
                    else
                    {
                        _DataDirectory = qbookName+ ".data";
                    }
                }
                return _DataDirectory;
            }
            set
            {
                _DataDirectory = value; // nicht auf null setzen
            }
        }

        string _BackupDirectory = null;
        public string BackupDirectory
        {
            get
            {
                if (_BackupDirectory == null)
                {
                    //create/use default data-directory
                    string qbookName = Core.ThisBook.Filename;
                    Match m = NameVersionExtRegex.Match(qbookName);
                    if (m.Success)
                    {
                        string name = m.Groups["name"].Value;
                        string version = m.Groups["version"].Value;
                        string ext = m.Groups["ext"].Value.ToLower();
                        if (ext == ".aBook")
                        {
                            string dir = Path.Combine(Core.ThisBook.Directory, name + ".backup");
                            if (!System.IO.Directory.Exists(dir))
                                System.IO.Directory.CreateDirectory(dir);

                            _BackupDirectory = dir;
                        }
                    }
                    else
                    {
                        _BackupDirectory = qbookName + ".backup";
                    }
                }
                return _BackupDirectory;
            }
            set
            {
                _BackupDirectory = null;// value;
            }
        }

        string _SettingsDirectory = null;
        public string SettingsDirectory
        {
            get
            {
                if (_SettingsDirectory == null)
                {
                    //create/use default data-directory
                    string qbookName = Core.ThisBook.Filename;
                    Match m = NameVersionExtRegex.Match(qbookName);
                    if (m.Success)
                    {
                        string name = m.Groups["name"].Value;
                        string version = m.Groups["version"].Value;
                        string ext = m.Groups["ext"].Value.ToLower();
                        if (ext == ".aBook")
                        {
                            string dir = Path.Combine(Core.ThisBook.Directory, name + ".settings");
                            if (!System.IO.Directory.Exists(dir))
                                System.IO.Directory.CreateDirectory(dir);

                            _SettingsDirectory = dir;
                        }
                    }
                    else
                    {
                        _SettingsDirectory = qbookName + ".settings";
                    }
                }
                return _SettingsDirectory;
            }
            set
            {
                _SettingsDirectory = value;
            }
        }

        string _TempDirectory = null;
        [XmlIgnore]
        public string TempDirectory
        {
            get
            {
                if (_TempDirectory == null)
                {
                    //create/use default data-directory
                    string qbookName = Core.ThisBook.Filename;
                    Match m = NameVersionExtRegex.Match(qbookName);
                    if (m.Success)
                    {
                        string name = m.Groups["name"].Value;
                        string version = m.Groups["version"].Value;
                        string ext = m.Groups["ext"].Value.ToLower();
                        if (ext == ".aBook")
                        {
                            string dir = Path.Combine(Core.ThisBook.Directory, name + ".temp");
                            if (!System.IO.Directory.Exists(dir))
                                System.IO.Directory.CreateDirectory(dir);

                            _TempDirectory = dir;
                        }
                    }
                    else
                    {
                        _TempDirectory = qbookName + ".temp";
                    }
                }
                return _TempDirectory;
            }
            set
            {
                _TempDirectory = value;
            }
        }

        public string _LogFilename = "qbook.log";
        public string LogFilename
        {
            get
            {

                return _LogFilename;
            }
            internal set
            {
              
            }
        }

        public string _Language = "en";
        public string Language
        {
            get
            {
                return _Language;
            }
            set
            {
                var changed = _Language != value;
                _Language = value;
                if (changed)
                {
                 //   OnPropertyChangedEvent("Language", _Language);
                }
            }
        }
    
 






    }
}
