//Import various C# things.
using System;
using System.IO;
using System.Text;
using System.Reflection;
using System.Collections.Generic;
using System.Data;
using System.Text.RegularExpressions;
using System.Net;
using System.Net.Mail;
using System.Net.Mime;

//Import Procon things.
using PRoCon.Core;
using PRoCon.Core.Plugin;
using PRoCon.Core.Players;
using PRoCon.Core.Maps;

namespace PRoConEvents
{
    public class WatchDog : PRoConPluginAPI, IPRoConPluginInterface
    {
        private bool pluginEnabled = false;
        private List<String> watchlist = new List<String>();
        private int debugLevel = 1;
        private string emailAddress = "";
        private string SMTPServer = "";
        private string SMTPUsername = "";
        private string SMTPPassword = "";
        enumBoolYesNo sendEmail;

        public WatchDog()
        {
            this.sendEmail = enumBoolYesNo.Yes;
        }

        public string GetPluginName()
        {
            return "WatchDog";
        }

        public string GetPluginVersion()
        {
            return "0.5.2";
        }

        public string GetPluginAuthor()
        {
            return "Analytalica";
        }

        public string GetPluginWebsite()
        {
            return "purebattlefield.org";
        }

        public string GetPluginDescription()
        {
            return @"WatchDog is a plugin for PRoCon that sends email alerts when a player on a configurable watchlist joins the server.";
        }

        public void toChat(String message)
        {
            toChat(message, "all");
        }

        public void toChat(String message, String playerName)
        {
            if (!message.Contains("\n") && !String.IsNullOrEmpty(message))
            {
                if (playerName == "all")
                {
                    this.ExecuteCommand("procon.protected.send", "admin.say", message, "all");
                }
                else
                {
                    this.ExecuteCommand("procon.protected.send", "admin.say", message, "player", playerName);
                }
            }
            else if (message != "\n")
            {
                string[] multiMsg = message.Split(new string[] { "\n" }, StringSplitOptions.None);
                foreach (string send in multiMsg)
                {
                    if (!String.IsNullOrEmpty(message))
                        toChat(send, playerName);
                }
            }
        }

        public void toConsole(int msgLevel, String message)
        {
            if (debugLevel >= msgLevel)
            {
                this.ExecuteCommand("procon.protected.pluginconsole.write", "WatchDog (" +  DateTime.Now.ToString("G") + "): " + message);
            }
        }

        public override void OnPlayerJoin(string soldierName)
        {
            if (pluginEnabled)
            {
                this.toConsole(3, "" + soldierName + " just joined...");
                if (watchlist.Contains(soldierName.Trim().ToLower()))
                {
                    this.toConsole(2, "Alert!");
                    this.toConsole(1, String.Format("{0}, who is a watched player, just joined the server.", soldierName.Trim()));
                    this.sendOutEmail(soldierName.Trim());
                }
            }
        }

        private void sendOutEmail(string offendor)
        {
            if (this.sendEmail == enumBoolYesNo.Yes && this.pluginEnabled)
            {
                this.toConsole(2, "Emailing process started.");
                
                try
                {
                    this.toConsole(2, String.Format("Using settings: SMTP Server {0}, SMTP Username {1}, SMTP Password {2}, Destination {3}", this.SMTPServer, this.SMTPUsername, this.SMTPPassword, this.emailAddress));
                    SmtpClient mySmtpClient = new SmtpClient(this.SMTPServer);

                    // set smtp-client with basicAuthentication
                    mySmtpClient.UseDefaultCredentials = false;
                    System.Net.NetworkCredential basicAuthenticationInfo = new
                    System.Net.NetworkCredential(this.SMTPUsername, this.SMTPPassword);
                    mySmtpClient.Credentials = basicAuthenticationInfo;

                    // add from,to mailaddresses
                    MailAddress from = new MailAddress("developers@purebattlefield.org", "WatchDog");
                    MailAddress to = new MailAddress(this.emailAddress, "Admins");
                    MailMessage myMail = new System.Net.Mail.MailMessage(from, to);

                    // set subject and encoding
                    myMail.Subject = "Test message";
                    myMail.SubjectEncoding = System.Text.Encoding.UTF8;

                    // set body-message and encoding
                    myMail.Body = "<b>Test Mail</b><br>using <b>HTML</b>.";
                    myMail.BodyEncoding = System.Text.Encoding.UTF8;
                    // text or html
                    myMail.IsBodyHtml = true;

                    mySmtpClient.Send(myMail);
                }
                catch (Exception ex)
                {
                    this.toConsole(1, "Email exception! " + ex);
                }
            }
        }

        public void OnPluginLoaded(string strHostName, string strPort, string strPRoConVersion)
        {
            this.RegisterEvents(this.GetType().Name, "OnPluginLoaded", "OnPlayerJoin");
        }

        public void OnPluginEnable()
        {
            this.pluginEnabled = true;
            this.toConsole(1, "WatchDog Enabled!");
        }

        public void OnPluginDisable()
        {
            this.pluginEnabled = false;
            this.toConsole(1, "WatchDog Disabled!");
        }

        public List<CPluginVariable> GetDisplayPluginVariables()
        {
            List<CPluginVariable> lstReturn = new List<CPluginVariable>();
            lstReturn.Add(new CPluginVariable("Player Watchlist|Add a soldier name... (ci)", typeof(string), ""));
            this.watchlist.Sort();
            for (int i = 0; i < watchlist.Count; i++ )
            {
                String thisPlayer = watchlist[i];
                if (String.IsNullOrEmpty(thisPlayer))
                {
                    watchlist.Remove(thisPlayer);
                    i--;
                }
                else
                {
                    lstReturn.Add(new CPluginVariable("Player Watchlist|" + i.ToString() + ". Soldier name:", typeof(string), thisPlayer));
                }
            }
            lstReturn.Add(new CPluginVariable("Email/SMTP Settings|Enable email alerts?", typeof(enumBoolYesNo), this.sendEmail));
            lstReturn.Add(new CPluginVariable("Email/SMTP Settings|SMTP Server", typeof(string), this.SMTPServer));
            lstReturn.Add(new CPluginVariable("Email/SMTP Settings|SMTP Username", typeof(string), this.SMTPUsername));
            lstReturn.Add(new CPluginVariable("Email/SMTP Settings|SMTP Password", typeof(string), this.SMTPPassword));
            lstReturn.Add(new CPluginVariable("Email/SMTP Settings|Destination Email Address", typeof(string), this.emailAddress));
            lstReturn.Add(new CPluginVariable("Other Settings|Debug Level", typeof(string), this.debugLevel.ToString()));
            return lstReturn;
        }

        public List<CPluginVariable> GetPluginVariables()
        {
            return GetDisplayPluginVariables();
        }

        public int getConfigIndex(string configString)
        {
            int lineLocation = configString.IndexOf('|');
            return Int32.Parse(configString.Substring(lineLocation + 1, configString.IndexOf('.') - lineLocation - 1));
        }

        public void SetPluginVariable(String strVariable, String strValue)
        {
            try
            {
                if (strVariable.Contains("Soldier name:"))
                {
                    int n = getConfigIndex(strVariable);
                    try
                    {
                        this.watchlist[n] = strValue.Trim().ToLower();
                    }
                    catch (ArgumentOutOfRangeException e)
                    {
                        this.watchlist.Add(strValue.Trim().ToLower());
                    }
                }
                else if (strVariable.Contains("Add a soldier name..."))
                {
                    this.watchlist.Add(strValue.Trim().ToLower());
                }
                else if (strVariable.Contains("Enable email alerts?") && Enum.IsDefined(typeof(enumBoolYesNo), strValue) == true)
                {
                    this.sendEmail = (enumBoolYesNo)Enum.Parse(typeof(enumBoolYesNo), strValue);
                    if (this.sendEmail == enumBoolYesNo.Yes)
                        this.toConsole(2, "Email alerts enabled.");
                    else
                        this.toConsole(2, "Email alerts disabled.");
                }
                else if (strVariable.Contains("SMTP Server"))
                {
                    this.SMTPServer = strValue.Trim();
                }
                else if (strVariable.Contains("SMTP Username"))
                {
                    this.SMTPUsername = strValue.Trim();
                }
                else if (strVariable.Contains("SMTP Password"))
                {
                    this.SMTPPassword = strValue.Trim();
                }
                else if (strVariable.Contains("Destination Email Address"))
                {
                    this.emailAddress = strValue.Trim().ToLower();
                }
                else if (strVariable.Contains("Debug Level"))
                {
                    try
                    {
                        this.debugLevel = Int32.Parse(strValue);
                    }
                    catch (Exception z)
                    {
                        this.toConsole(1, "Invalid debug level! Use integer values only.");
                        this.debugLevel = 1;
                    }
                }
            }
            catch (Exception e)
            {
                this.toConsole(1, e.ToString());
            }
        }
    }
}