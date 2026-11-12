using ARSTLib;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Schema;
using static System.Net.WebRequestMethods;

namespace ttlt
{
    internal class Program
    {
        static int liveTime = 0, liveTimerCounter = 0, lastCheckHour = 0;
        static string domain = "", ver = "0.7_preI1";
        static bool isInitialized = false;

        static aConfg confg = new aConfg();
        static ARSTLogAPI log = new ARSTLogAPI();

        static void Main(string[] args)
        {
            try
            {
                Console.WriteLine("Initializing tunnel timer host...");
                confg.init("/home/arst/ttlt/tunnelConfig.txt");
                log.init("/home/arst/ttlt/tunnelLog.txt", "ARST Tunnel");
                log.autoApplyToFile = true;
                log.info($"System by ARST mechanic studio Inc, v{ver}");
                log.info("Initializing started.");
                log.info("Reading configuration...");
                liveTime = Convert.ToInt32(confg.read("liveTime"));
                domain = confg.read("domain");
                log.addToLog($"---\nliveTime={liveTime}\ndomain='{domain}'\n---", false);
                
                log.info("Main Initialized!");
                new Thread(() => { startTunnel(); }).Start();
                while (true) Thread.Sleep(1000);
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
            log.info("Checking for process 'lt'...");
            var runningProcs = from proc in Process.GetProcesses(".") orderby proc.Id select proc;
            if (runningProcs.Count(p => p.ProcessName.Contains("lt")) > 0) return true;
            return false;
        }

        static bool checkDomain(string tdFile)
        {
            string targetDomain = getTargetDomain(tdFile);
            log.info($"Verifying domains... [domain='{domain}', targetDomain='{targetDomain}']");
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
                    int I = 0, mtri = 5;

                    while (true)
                    {
                        if (I > mtri)
                        {
                            startProcess("sudo systemctl stop ttlc");
                            throw new Exception($"Tunnel restart overcount! [domain='{domain}', I={I}]");
                        }
                        else
                        {
                            I++;
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
                            startProcess("sudo rm " + tdFile);
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

        static void restartTunnel()
        {
            log.info("Restarting tunnel...");
            startProcess("sudo systemctl restart arsttunnel");
        }

        static void tunnelDone(string tdFile, string targetDomain, bool already = false)
        {
            if(already) log.info($"Tunnel already created done: {targetDomain}");
            else log.info("Tunnel created done: " + targetDomain);

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
                                int currentHour = liveTimerCounter / 60;
                                if (lastCheckHour != currentHour)
                                {
                                    lastCheckHour = currentHour;
                                    log.info($"Timer hour counted({liveTimerCounter}:{currentHour})");
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
                    log.info("Process started!");
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