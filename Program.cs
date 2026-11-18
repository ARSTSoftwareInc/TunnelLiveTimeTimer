using ARSTLib;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Schema;
using static System.Net.WebRequestMethods;

namespace ttlt
{
    internal class Program
    {
        static int liveTime = 0, liveTimerCounter = 0, lastCheckHour = 0, maxTunnelRestartCount = 5;
        static string domain = "", ver = "0.9_preI1";
        static bool isInitialized = false;

        static aConfg confg = new aConfg();
        static ARSTLogAPI log = new ARSTLogAPI();

        static void Main(string[] args)
        {
            try
            {
                Console.WriteLine($"Initializing tunnel timer host v{ver} ...");
                if (args.Length == 0)
                {
                    Console.WriteLine("\n*** Arguments list empty ***\n");
                    confg.init("/home/arst/ttlt/tunnelConfig.txt");
                    log.init("/home/arst/ttlt/tunnelLog.txt", "ARST Tunnel");
                    log.autoApplyToFile = true;
                    log.info($"System by ARST mechanic studio Inc, (C) 2026");
                    log.info($"System version: {ver}\n");
                    log.info("Initializing started.");
                    log.info("Reading configuration...");
                    liveTime = Convert.ToInt32(confg.read("liveTime"));
                    domain = confg.read("domain");
                    maxTunnelRestartCount = Convert.ToInt32(confg.read("maxTRC"));
                    log.addToLog($"---\nliveTime={liveTime}\ndomain='{domain}'\n---", false);

                    log.info("Main Initialized!");
                    new Thread(() => { startTunnel(); }).Start();
                    while (true) Thread.Sleep(1000);
                }
                else
                {
                    string arg = args[0];
                    Console.WriteLine("Typed: " + arg);
                    switch (arg)
                    {
                        case "help":
                            Console.WriteLine("\n+++ Commands help +++\nhelp - entering help for commands;\n-restartTunnel - restarts tunnel and reset timer");
                            break;
                        case "restartTunnel":
                            Console.WriteLine("Initiating tunnel restart...");
                            Console.WriteLine("Stoping arsttunnel...");
                            Process.Start("sudo","systemctl stop arsttunnel");
                            Thread.Sleep(400);
                            Process.Start("systemctl", "status arsttunnel");
                            Console.WriteLine("\nRestarting ttlc...");
                            Process.Start("sudo", "systemctl restart ttlc");
                            Thread.Sleep(400);
                            Process.Start("systemctl", "status ttlc");
                            break;
                    }

                    Console.WriteLine("\n\nDone!");
                }
            }
            catch(Exception ex)
            {
                error(ex.Message, "INIT_ERROR", true);
            }
        }

        static string getTargetDomain(string tdFile)
        {
            if (!System.IO.File.Exists(tdFile)) return "FILE_NOT_FOUND";
            string targetDomain = System.IO.File.ReadAllText(tdFile, Encoding.UTF8);
            targetDomain = targetDomain.Trim();
            return targetDomain;
        }

        static bool isProcLTExists()
        {
            log.info("Checking for process 'node' ...");
            var processes = Process.GetProcessesByName("node");
            try 
            {
                bool exists = processes.Length > 0;
                if(exists) log.info("Process exists.");
                else log.info("Process not exists.");

                return exists; 
            }
            finally { foreach (var proc in processes) { proc.Dispose(); } }
        }

        static bool checkDomain(string tdFile)
        {
            string targetDomain = getTargetDomain(tdFile);
            log.info($"Verifying domains and checking tunnel process... [domain='{domain}', targetDomain='{targetDomain}']");
            return domain == targetDomain && isProcLTExists();
        }

        static void startTunnel()
        {
            try
            {
                string tdFile = "/var/tmp/tdf/tunnel_url.txt";
                log.info("Checking tunnel...");
                if (checkDomain(tdFile) && !isInitialized) tunnelDone(tdFile, getTargetDomain(tdFile), true);
                else
                {
                    int I = 0;

                    while (true)
                    {
                        if (I > maxTunnelRestartCount)
                        {
                            startProcess("sudo systemctl stop ttlc");
                            throw new Exception($"Tunnel restart overflow! [domain='{domain}', I={I}]");
                        }
                        else
                        {
                            I++;
                            tdFileDel(tdFile);
                            Thread.Sleep(440);
                            log.info($"Pending tunnel(try count:{I})...");
                            if (I == 1 && !isInitialized)
                            {
                                if (!isProcLTExists()) startProcess("sudo systemctl start arsttunnel");
                                else if (!checkDomain(tdFile)) restartTunnel();
                            }
                            else restartTunnel();

                            log.info($"Tunnel data awaiting... [tdFile='{tdFile}']");
                            while (true) { if (System.IO.File.Exists(tdFile)) break; }
                            Thread.Sleep(700);
                            if (checkDomain(tdFile))
                            {
                                tunnelDone(tdFile, getTargetDomain(tdFile));
                                break;
                            }
                            else log.info("Domains dont match. Await to rechecking...");
                        }

                        Thread.Sleep(10500); // Ожидание 10 секунд и 500 мс перед новой итерацией перезапуска тоннеля
                    }
                }
            }
            catch(Exception ex)
            {
                error(ex.Message, "START_TUNNEL_ERROR");
            }
        }

        static void tdFileDel(string tdFile)
        {
            log.info($"Deleting tunnel data file... [tdFile='{tdFile}']");
            startProcess("sudo rm " + tdFile);
        }

        static void restartTunnel()
        {
            log.info("Restarting tunnel...");
            startProcess("sudo systemctl restart arsttunnel");
        }

        static void tunnelDone(string tdFile, string targetDomain, bool already = false)
        {
            if(already) log.info($"Tunnel already created done: {targetDomain}");
            else log.info("Tunnel created done: " + targetDomain);

            tdFileDel(tdFile);
            System.IO.File.WriteAllText(Path.Combine(Path.GetDirectoryName(tdFile), "tdf.txt"), targetDomain);
            
            if (!isInitialized)
            {
                log.info("First tunnel initialized.");
                isInitialized = true;
                new Thread(() =>
                {
                    log.info("Pending live timer...");
                    if(liveTime > 500) log.info("Counting live timer...");
                    System.Timers.Timer timer = new System.Timers.Timer(1000);
                    timer.Elapsed += (object sender, System.Timers.ElapsedEventArgs e) =>
                    {
                        try
                        {
                            liveTimerCounter++;
                            
                            if (liveTime <= 500) log.info("Seconds to restart: " + (liveTime - liveTimerCounter).ToString());
                            else
                            {
                                int currentHour = liveTimerCounter / 3600;
                                if (lastCheckHour != currentHour)
                                {
                                    lastCheckHour = currentHour;
                                    log.info($"Timer hour counted({liveTimerCounter}:{currentHour}).");
                                }
                            }

                            if (liveTimerCounter > liveTime)
                            {
                                liveTimerCounter = 0;
                                log.info("Timer overflowed: " + liveTime);
                                log.info("Restarting tunnel...");
                                startTunnel();
                            }
                        }
                        catch (Exception ex)
                        {
                            timer.Stop();
                            error(ex.Message, "LIVE_TIMER_ERROR");
                        }
                    };

                    timer.Start();
                }).Start();
            }
        }

        static void startProcess(string cmd)
        {
            log.info("Initiating start command: " + cmd);
            string name = "", args = "";

            string[] dat = cmd.Split(' ');
            name = dat[0];
            if (dat.Length > 1) args = cmd.Remove(0, name.Length + 1);

            log.info($"Starting process... [name='{name}', args='{args}']");
            Task.Run(() => {
                try
                {
                    var startInfo = new ProcessStartInfo
                    {
                        FileName = name,
                        Arguments = args,
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = false
                    };

                    using (Process process = new Process { StartInfo = startInfo }) { process.Start(); }
                    log.info("Process started: " + name);
                }
                catch (Exception ex)
                {
                    throw new Exception("START_PROCESS_ERROR::" + ex.Message);
                }
            });
        }

        static void error(string text, string code, bool isCritic = false)
        {
            if (isCritic)
            {
                startProcess("sudo systemctl stop ttlc");
                log.error($"Internal critic error::{code}: {text}");
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Internal critic error::{code}: {text}");
                Console.ForegroundColor = ConsoleColor.White;
                Environment.Exit(0);
            }
            else log.warn($"Exception::{code}: {text}");
        }
    }
}