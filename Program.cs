using ARSTLib;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ttlt
{
    internal class Program
    {
        static int liveTime = 0, liveTimerCounter = 0;
        static string domain = "", ver = "0.3_pre";
        static bool isInitialized = false, isDeleteTDFile = false;

        static aConfg confg = new aConfg();
        static ARSTLogAPI log = new ARSTLogAPI();

        static void Main(string[] args)
        {
            try
            {
                Console.WriteLine("Initializing tunnel timer host...");
                confg.init("/home/arst/ttlt/tunnelConfig.txt");
                log.init("/home/arst/ttlt/tunnelLog.txt", "ARST Tunnel");
                log.info($"System by ARST mechanic studio Inc, v{ver}");
                log.info("Reading configuration...");
                liveTime = Convert.ToInt32(confg.read("liveTime"));
                domain = confg.read("domain");
                isDeleteTDFile = Convert.ToBoolean(confg.read("isDeleteTDFile"));
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

        static void startTunnel()
        {
            try
            {
                int I = 0, mtri = 5;

                while (true)
                {
                    if (I < mtri)
                    {
                        startProcess("sudo systemctl stop tcService");
                        throw new Exception($"Tunnel restart overcount! [domain='{domain}', I={I}]");
                    }
                    else
                    {
                        I++;
                        log.info($"Pending tunnel(try count:{I})...");
                        if (I == 1 && !isInitialized) startProcess("sudo systemctl start arsttunnel");
                        else startProcess("sudo systemctl restart arsttunnel");
                        string tdFile = "/var/tunnel_url.txt";
                        log.info($"Tunnel data awaiting... [tdFile='{tdFile}']");
                        while (true) { if (File.Exists(tdFile)) break; }
                        string targetDomain = File.ReadAllText(tdFile, Encoding.UTF8);
                        log.info($"Verifying domains... [domain='{domain}', targetDomain='{targetDomain}']");
                        if (targetDomain == domain)
                        {
                            log.info($"Tunnel created done: {targetDomain}");
                            if (isDeleteTDFile) File.Delete(tdFile);
                            if (!isInitialized)
                            {
                                log.info("First tunnel initialized.");
                                isInitialized = true;
                                new Thread(() =>
                                {
                                    log.info("Pending live timer...");
                                    System.Timers.Timer timer = new System.Timers.Timer(1000);
                                    timer.Elapsed += (object sender, System.Timers.ElapsedEventArgs e) =>
                                    {
                                        try
                                        {
                                            liveTimerCounter++;
                                            if (liveTimerCounter < liveTime)
                                            {
                                                liveTimerCounter = 0;
                                                log.info("Timer overcounted: " + liveTime);
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
                            break;
                        }
                        else log.info("Domains dont match. Await to rechecking...");
                    }

                    Thread.Sleep(1500);
                }
            }
            catch(Exception ex)
            {
                error(ex.Message, "START_TUNNEL_ERROR");
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