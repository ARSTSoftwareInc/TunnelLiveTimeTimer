// ARSTLog ver 3.0

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.CompilerServices.RuntimeHelpers;

namespace ARSTLib
{
    internal class ARSTLogAPI
    {
        string _logPath = "", appIndentifier = "NULL";
        public int counter = 0, maxCounter = 20000, lastCode = 0;
        public bool autoApplyToFile = false;

        StringBuilder log = new StringBuilder();

        public void init(string logPath, string appName, int maxLogCounter = 0)
        {
            try
            {
                if (logPath == "") throw new Exception("Путь к лог файлу должен быть не пуст");
                _logPath = logPath;
                if(appName != "") appIndentifier = appName;
                //isAutoSaveToAnotherLogEnabled = autoSaveToAnotherLog;
                if (maxLogCounter != 0) maxCounter = maxLogCounter;

                Console.WriteLine($"[{appIndentifier}] ARSTLog --- init:: maxCounter=" + maxCounter);
                Console.WriteLine($"[{appIndentifier}] ARSTLog --- init:: logPath='{logPath}'");
                Console.WriteLine($"[{appIndentifier}] ARSTLog --- init:: appIdentifier='{appIndentifier}'");
            }
            catch (Exception ex)
            {
                throw new Exception("ARSTLog api error trace::init(): " + ex.ToString());
            }
        }

        public void saveLog(string logPath = "")
        {
            try
            {
                if (logPath != "") _logPath = logPath;
                File.WriteAllText(_logPath, log.ToString());
            }
            catch(Exception ex)
            {
                throw new Exception("ARSTLog api error trace::saveLog(): " + ex.ToString());
            }
        }

        public void warn(string text)
        {
            lastCode = 1;
            addToLog("WARNING: " + text);
        }

        public void error(string text)
        {
            lastCode = 2;
            addToLog("ERROR: " + text);
        }

        public void info(string text)
        {
            addToLog("INFO: " + text);
        }

        public void addToLog(string text, bool showDate = true)
        {
            if(counter > maxCounter)
            {
                counter = 0;
                log.Clear();
                log.Append($"-str:{counter}- [{DateTime.Now.ToString()}] log automaticaly cleared!\n\n");
                Console.WriteLine($"[{appIndentifier}] [{DateTime.Now.ToString()}] ARSTLog --- addToLog:: log automaticaly cleared!");               
                
                saveLog();
            }
            else 
            {
                if (showDate) text = $"-str:{counter}- [{DateTime.Now.ToString()}] " + text;
                log.Append($"{text}\n");
                Console.WriteLine($"[{appIndentifier}] {text}");

                if (autoApplyToFile)
                {
                    if (lastCode == 2) saveLog(_logPath.Replace(Path.GetExtension(_logPath), "") + "_err" + Path.GetExtension(_logPath));
                    else saveLog();
                }

                counter++;
            }

            lastCode = 0;
        }
    }
}