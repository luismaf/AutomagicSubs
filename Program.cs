﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.IO;
using System.Net;
using CookComputing.XmlRpc;

namespace AutomagicSubs
{

    class Program
    {
		private static IOSDb osdbProxy;
		private static string DefaultLang = ConfigurationManager.AppSettings["DefaultLang"];
		private static bool OverwriteByDefault = ConfigurationManager.AppSettings["OverwriteByDefault"] == "true" ? true : false;
		private static bool NoLangByDefault = ConfigurationManager.AppSettings["NoLangByDefault"] == "true" ? true : false;
        private static string CDFormat = ConfigurationManager.AppSettings["CDFormat"];
        private static string FileFormat = ConfigurationManager.AppSettings["FileFormat"];
        private static string FolderFormat = ConfigurationManager.AppSettings["FolderFormat"];
        private static string theToken;

        private static List<string> movieFormats = new List<string>(ConfigurationManager.AppSettings["MovieFormat"].Split(','));// { ".srt", ".sub", ".smi", ".txt" };
        private static List<string> subtitleFormats = new List<string>(ConfigurationManager.AppSettings["SubtitleFormats"].Split(','));// { ".srt", ".sub", ".smi", ".txt" };
        private static string[] langs;
        private static List<langMap> theLangMap = Utils.generateLangMap();
        private static int dlCount = 0;
        private static List<MovieFile> myFiles = new List<MovieFile>();

        static void Main(string[] args)
        {
            if (args.Length == 0)
            {
				Console.WriteLine("AutomagicSubs: Forked by luismaf form vrokolos (vrokolos@gmail.com) VrokSub");
				Console.WriteLine("");
				Console.WriteLine("Simple command line program that automatically downloads subtitles for your movies. You just pass a folder path and a preferred language list and it searches for subtitles using your avi files. It uses opensubtitles for the search and it requires the .net 2 framework runtimes which you probably already have.");
                Console.WriteLine("");
                Console.WriteLine("Usage:");
                Console.WriteLine("");
                Console.WriteLine("AutomagicSubs.exe \"[Folder Path]\" [Language Code Sequence] [Params]");
                Console.WriteLine("");
                Console.WriteLine("Folder Path: The path to the folder that has all your movies. AutomagicSubs will search all subfolders of this path for movies.");
                Console.WriteLine("");
				Console.WriteLine("Language Code Sequence: A sequence of two letter language codes according to your preference separated by coma. By default will be use the language set in config file. AutomagicSubs will search subtitles of the first language code and if it doesn't find one it will continue to the next code. You can find the two letter codes here: http://www.loc.gov/standards/iso639-2/php/code_list.php.");
                Console.WriteLine("");
                Console.WriteLine("Params:");
                Console.WriteLine("");
				Console.WriteLine(" /nolangtag will not add the language to the subtitile filename");
                Console.WriteLine(" /rename will rename all the movies for which AutomagicSubs has found a subtitle using the format found in config file");
                Console.WriteLine(" /newonly will only try to locate subtitles for movies without subtitles and ignore the ones that have subtitles");
                Console.WriteLine(" /nfo will download data from imdb.com (like actors, directors etc) and save them to a nfo file named like your movie");
                Console.WriteLine(" /covers will download the cover images imdb uses and save them to a jpg file named like your movie");
                Console.WriteLine(" /folders will create a folder for each movie and move all files there. If /covers is used with this then a folder.jpg will also be created");
                Console.WriteLine(" /nosubfolders will not search every subfolder of the given folder for movie files");
				Console.WriteLine(" /move=\"[Output Path]\" will move all files and folders to the given path. Useful if combined with folders");
                Console.WriteLine(" /pause will not exit the app until you press a key");
                Console.WriteLine("");
                Console.WriteLine("Examples:");
                Console.WriteLine("AutomagicSubs.exe \"c:\\my videos\" es,en");
                Console.WriteLine(" This will first try to locate a spanish subtitle for every movie in c:\\my videos (including subfolders) and if it doesn't find one it will try to find one in english. You can use more language codes if you'd like.");
                Console.WriteLine("");
                Console.WriteLine("AutomagicSubs.exe \"c:\\my videos\" it /rename");
                Console.WriteLine(" will also rename all movies where subtitle has been found with year and cd number: MovieName(Year)-CD2.avi");
                Console.WriteLine("");
                Console.WriteLine("AutomagicSubs.exe \"c:\\my videos\" de /newonly");
                Console.WriteLine(" will only search for subtitles for movies that don't have one already.");
                Console.WriteLine("");
                Console.WriteLine("AutomagicSubs.exe \"c:\\unsubbedvideos\" nl /rename /folders /nfo /covers /move=\"c:\\my videos\"");
                Console.WriteLine(" 1) Creates a folder for each movie under c:\\my videos with the name format found in AutomagicSubs.exe.config");
                Console.WriteLine(" 2) Renames each movie using the format found in AutomagicSubs.exe.config");
                Console.WriteLine(" 3) Downloads dutch subtitles for each movie");
                Console.WriteLine(" 4) Downloads imdb details and saves them to the output folder");
                Console.WriteLine(" 5) Downloads imdb covers and saves them to the output folder as folder.jpg and movie.jpg");
                Console.WriteLine(" Note: Only movies with found subtitles will be affected");
				Console.WriteLine("");
                //Console.ReadLine();
            }
            else
            {
                List<string> argList = new List<string>(args);
                argList.ForEach(new Action<string>(tolower));
                bool ren = false;
                bool noLangTag = false;
                bool newOnly = false;
                bool nfo = false;
                bool folders = false;
                bool imdb = false;
                bool covers = false;
                bool pause = false;
                string inputPath = "";
                string outputPath = "";
                string langSeq = "";
                foreach (string arg in argList)
                {
                    if (arg.StartsWith("/move="))
                    {
                        outputPath = arg.Replace("/move=", "");
                    }
                }
                if (args.Length > 2)
                {
                    ren = (argList.Contains("/rename"));
                    noLangTag = (argList.Contains("/nolangtag"));
                    newOnly = (argList.Contains("/newonly"));
                    nfo = (argList.Contains("/nfo"));
                    folders = (argList.Contains("/folders"));
                    covers = (argList.Contains("/covers"));
                    pause = (argList.Contains("/pause"));
                    imdb = (nfo || covers);
                    //                    imdb = (argList.Contains("/imdb")) || covers;

                    argList.Remove("/rename");
                    argList.Remove("/nolangtag");
                    argList.Remove("/newonly");
                    argList.Remove("/nfo");
                    argList.Remove("/folders");
                    argList.Remove("/covers");
                    argList.Remove("/pause");
                    argList.Remove("/move=" + outputPath);
                    if (Directory.Exists(args[1]) && (!Directory.Exists(args[0])))
                    {
                        inputPath = args[1];
                        langSeq = args[0];
                    }
                    else
                    {
                        inputPath = args[0];
                        langSeq = args[1];
                    }
					Go(inputPath, outputPath, Get2CodeStr(langSeq), OverwriteByDefault, ren, noLangTag, newOnly, nfo, folders, imdb, covers, pause, false);
                }
				if (args.Length == 1 && Directory.Exists(args[0])) Go(args[0], outputPath, Get2CodeStr(DefaultLang), false, ren, NoLangByDefault, newOnly, nfo, folders, imdb, covers, pause, false);
            }
        }

        private static void tolower(string a)
        {
            a = a.ToLower();
        }

        private static string Get3CodeStr(string the2CodeStr)
        {
            string c3 = "";
            foreach (string l2code in the2CodeStr.Split(','))
            {
                foreach (langMap l in theLangMap)
                {
                    if (l.two.Contains(l2code))
                    {
                        if (!c3.Contains(l.three))
                        {
                            c3 += "," + l.three;
                        }
                    }
                }
            }
            if (c3 != "") { c3 = c3.Substring(1); }
            return c3;
        }

        private static string Get2CodeStr(string the2CodeStr)
        {
            string c3 = "";
            foreach (string l2code in the2CodeStr.Split(','))
            {
                foreach (langMap l in theLangMap)
                {
                    if (l.two.Contains(l2code))
                    {
                        if (!c3.Contains(l.two))
                        {
                            c3 += "," + l.two;
                        }
                    }
                }
            }
            if (c3 != "") { c3 = c3.Substring(1); }
            return c3;
        }

        private static void Go(string FolderArg, string outputPath, string LangArg, bool overwrite, bool rename, bool noLangTag, bool newOnly, bool nfo, bool folders, bool imdb, bool covers, bool pause, bool nosubfolders)
        {
            try
            {
            Console.WriteLine("Searching for movie files");
            #region Get Movie Files
            if (File.Exists(FolderArg))
            {
                if (outputPath == "")
                {
                    outputPath = Path.GetDirectoryName(FolderArg);
                }
                MovieFile theFile = new MovieFile();
                theFile.filename = FolderArg;
                theFile.getOldSubtitle(subtitleFormats, theLangMap, LangArg);
                myFiles.Add(theFile);
            }
            else if (Directory.Exists(FolderArg))
            {
                if (outputPath == "")
                {
                    outputPath = FolderArg;
                }
                foreach (string extension in movieFormats)
                {
                    foreach (string filename in Utils.GetFilesByExtensions(FolderArg, "." + extension, SearchOption.AllDirectories))
                    {
                        MovieFile theFile = new MovieFile();
                        theFile.filename = filename;
                        theFile.getOldSubtitle(subtitleFormats, theLangMap, LangArg);

                        if ((newOnly && theFile.oldSubtitle != "") || (!newOnly))
                        {
                            myFiles.Add(theFile);
                        }
                    }
                }
                Console.WriteLine("Found " + myFiles.Count.ToString() + " Movie Files");
            }
            else
            {
                throw new Exception("The folder or file given does not exist");
            }
            #endregion
            if (myFiles.Count != 0)
            {

                Console.WriteLine("Connecting...");
                #region Connect and login
                osdbProxy = XmlRpcProxyGen.Create<IOSDb>();
                osdbProxy.Url = "http://api.opensubtitles.org/xml-rpc";
                //osdbProxy.KeepAlive = false;
                osdbProxy.KeepAlive = true;
                if (myFiles.Count == 0) { Console.WriteLine("No movies found"); }
                XmlRpcStruct Login = osdbProxy.LogIn("", "", "en", "vroksub");
                theToken = Login["token"].ToString();
                #endregion

                Console.WriteLine("Creating subtitle request from movie files");
                #region Create subtitle request from movie files
                langs = Get3CodeStr(LangArg).Split(',');
                List<string> l3 = new List<string>(langs);
                //List<string> l2 = new List<string>(LangArg.Split(',')); //commented by luismaf
                int i = 0;
                List<List<subInfo>> allSis = new List<List<subInfo>>();
                List<subInfo> sis = new List<subInfo>();
                foreach (MovieFile theFile in myFiles)
                {
                    if ((newOnly && (theFile.oldSubtitle == "")) || (!newOnly))
                    {
                        try
                        {
                            theFile.hash = Utils.ToHexadecimal(Utils.ComputeMovieHash(theFile.filename));
                            theFile.bytesize = new FileInfo(theFile.filename).Length.ToString();
                            
                            foreach (string lang in langs)
                            {
                                if (sis.Count >= 40)
                                {
                                    allSis.Add(sis);
                                    sis = new List<subInfo>();
                                }
                                subInfo si = new subInfo();
                                si.moviehash = theFile.hash;
                                si.moviebytesize = theFile.bytesize;
                                si.tag = theFile.filename; //added by luismaf
                                si.sublanguageid = lang;
                                sis.Add(si);
                                i++;                           
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine("Error with file: " + theFile.filename + "\n" + ex.Message);
                        }
                    }
                }
                if (sis.Count > 0)
                {
                    allSis.Add(sis);
                }
                #endregion

                Console.WriteLine("Searching for subtitles...");
                #region Search for subtitles
                subrt SubResults = new subrt();
                List<subRes> lstSubResults = new List<subRes>();
                foreach (List<subInfo> theSis in allSis) 
                {
                    try
                    {
                        subrt tempSubResults = osdbProxy.SearchSubtitles(theToken, theSis.ToArray());
                        //subrt tempSubResults = osdbProxy.SearchSubtitles(theToken, theSis.ToArray(), "500");
						lstSubResults.AddRange(tempSubResults.data);
                        SubResults.seconds += tempSubResults.seconds;
					}
                    catch (Exception)
                    {
							//Console.WriteLine("Error: " + e.Message.ToString()); 
							//commented by luismaf to avoid "Error: response contains boolean value where array expected [response : struct mapped to type subrt : member data mapped to type subRes[]]"
                    }
                }
                if(lstSubResults.Count>0)
                    SubResults.data = lstSubResults.ToArray();
                
                #endregion

                #region Choose best subtitle
                //if (SubResults != null) mod by luismaf:
                if (SubResults.data != null)
                {
                    Console.WriteLine("Found subtitles:");
                    BindingList<subRes> g = new BindingList<subRes>(SubResults.data);
                    dlCount = 0;
                    foreach (MovieFile mf in myFiles)
                    {
                        try
                        {
                            if ((newOnly && (mf.oldSubtitle == "")) || (!newOnly))
                            {
                                if (mf.SelectBestSubtitle(g, l3))
                                {
                                    Console.WriteLine(mf.subRes.MovieName + " - " + mf.subRes.LanguageName);
                                    dlCount++;
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine("Could not choose subtitle for: " + mf.filename + "\n" + ex.Message);
                        }
                    }
                #endregion

                    Console.WriteLine("Downloading subtitles...");
                    #region Download Subtitles
                    //string[] ids = new string[dlCount]; //commented by luismaf
                    List<string> lstids = new List<string>();
                    List<List<string>> allids = new List<List<string>>();
                    int k = 0;
                    foreach (MovieFile myf in myFiles)
                    {
                        if (lstids.Count >= 40)
                        {
                            allids.Add(lstids);
                            lstids = new List<string>();
                        }
                        if (myf.subtitleId != null)
                        {
                            lstids.Add(myf.subtitleId);
                            k++;
                        }
                    }
                    if (lstids.Count > 0)
                    {
                        allids.Add(lstids);
                    }
                    subdata files = new subdata();
                    List<subtitle> thesubs = new List<subtitle>();
                    foreach (List<string> theList in allids)
                    {
                        subdata tempfiles = osdbProxy.DownloadSubtitles(theToken, lstids.ToArray());
                        thesubs.AddRange(tempfiles.data);
                        files.seconds += tempfiles.seconds;
                    }
                    files.data = thesubs.ToArray();
                    #endregion

                    if (imdb)
                    {
                        Console.WriteLine("Fetching imdb details...");
                        #region Fetch imdb details
                        foreach (MovieFile myf in myFiles)
                        {
                            if (myf.subRes != null)
                            {
                                try
                                {
                                    myf.imdbinfo = osdbProxy.GetIMDBMovieDetails(theToken, "0" + myf.subRes.IDMovieImdb).data;
                                }
                                catch (Exception ex)
                                {
                                    Console.WriteLine("Error fetching imdb data for: " + myf.filename + "\n" + ex.Message);
                                }
                            }
                        }
                        #endregion
                    }

                    Console.WriteLine("Saving subtitles...");

                    #region Process (rename, create folders, savenfo and save) subtitles
                    foreach (subtitle s in files.data)
                    {
                        foreach (MovieFile m in myFiles)
                        {
                            /*try
                            {//*/
                            if (m.subtitleId == s.idsubtitlefile)
                            {
                                m.subtitle = Utils.DecodeAndDecompress(s.data);
                                if (outputPath != FolderArg)
                                {
                                    if (!Directory.Exists(outputPath))
                                    {
                                        Directory.CreateDirectory(outputPath);
                                    }
									
									if (!File.Exists(outputPath + Path.DirectorySeparatorChar + Path.GetFileName(m.filename)))
                                    {
                                        try
                                        {
                                            File.Move(m.filename, outputPath + Path.DirectorySeparatorChar + Path.GetFileName(m.filename));
                                            m.originalfilename = Path.GetFileName(m.filename);
                                            m.filename = outputPath + Path.DirectorySeparatorChar + Path.GetFileName(m.filename);

                                        }
                                        catch (Exception ex)
                                        {
                                            Console.WriteLine("Error moving movie: " + m.filename + "\n" + ex.Message);
                                        }
                                    }
                                }
                                if (folders)
                                {
                                    m.newFolder(outputPath, FolderFormat);
                                }
                                if (rename)
                                {
                                    m.rename(FileFormat, CDFormat);
                                }
                                if (nfo)
                                {
                                    m.saveNfo();
                                }
                                m.saveSubtitle(overwrite, noLangTag);
                                continue;
                            }
                            /*}
                          catch (Exception ex)
                           {
                               Console.WriteLine("Error saving subtitle for: " + m.filename + "\n" + ex.Message);
                           }//*/
                        }
                    }
                }
                    #endregion

                if (covers)
                {
                    Console.WriteLine("Downloading covers...");
                    #region Download covers
                    System.Net.WebClient Client = new WebClient();

                    foreach (MovieFile myf in myFiles)
                    {
                        if (myf.imdbinfo != null)
                        {
                            if ((myf.imdbinfo.cover != null) && (myf.imdbinfo.cover != ""))
                            {
                                try
                                {
                                    Stream strm = Client.OpenRead(myf.imdbinfo.cover);
                                    FileStream writecover = new FileStream(Path.GetDirectoryName(myf.filename) + Path.DirectorySeparatorChar + Path.GetFileNameWithoutExtension(myf.filename) + ".jpg", FileMode.Create);
                                    int a;
                                    do
                                    {
                                        a = strm.ReadByte();
                                        writecover.WriteByte((byte)a);
                                    }
                                    while (a != -1);
                                    writecover.Position = 0;
										File.Copy(Path.GetDirectoryName(myf.filename) + Path.DirectorySeparatorChar + Path.GetFileNameWithoutExtension(myf.filename) + ".jpg", Path.GetDirectoryName(myf.filename) + Path.DirectorySeparatorChar + Path.GetFileName(myf.filename) + ".jpg");
                                    if (folders)
                                    {
                                        File.Copy(Path.GetDirectoryName(myf.filename) + Path.DirectorySeparatorChar + Path.GetFileNameWithoutExtension(myf.filename) + ".jpg", Path.GetDirectoryName(myf.filename) + "\\folder.jpg");
                                    }
                                    strm.Close();
                                    writecover.Close();
                                }
                                catch (Exception ex)
                                {
                                    Console.WriteLine("Error saving cover for: " + myf.filename + "\n" + ex.Message);
                                }
                            }
                        }
                    #endregion
                    }
                }

                Console.WriteLine("Disconnecting...");
                #region Disconnect
                osdbProxy.LogOut(theToken);
                #endregion
            }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error: " + ex.Message);
            }
            System.Threading.Thread.Sleep(300);
			Console.Write("Hope it has been helpful");
            System.Threading.Thread.Sleep(120);
            Console.Write(".");
            System.Threading.Thread.Sleep(120);
            Console.Write(".");
            System.Threading.Thread.Sleep(120);
            Console.Write(".");
            System.Threading.Thread.Sleep(200);
			Console.Write ("\n");
            //if (pause == true) Console.ReadKey();
			System.Environment.Exit(0);
        }
    }
}