//Import various C# things.
using System;
using System.IO;
using System.Text;
using System.Reflection;
using System.Collections.Generic;
using System.Data;
using System.Text.RegularExpressions;
using System.Threading;
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

		private string watchlistFilepath = "";
        private List<String> watchlist = new List<String>();
        private DateTime watchlistTime = DateTime.Now;
        private string[] watchlistLines = { };
        private int debugLevel = 1;
        private string emailAddress = "";
        private string SMTPServer = "";
        private string SMTPUsername = "";
        private string SMTPPassword = "";
        private int SMTPPort = 587;
        private string serverName = "";
        enumBoolYesNo sendEmail;
        enumBoolYesNo ssl;

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
            return "1.0.7";
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
            return @"<p>WatchDog is a plugin for PRoCon that sends email alerts when a player on a configurable watchlist joins the server.</p>
<p><b>Watchlist file:</b> put an absolute path for a text file containing a newline-separated list of player names to watch. This feature is optional; leave the field blank when not in use.
<p><b>To add players:</b> type names into the 'Add a soldier name...' field and they will automatically be alphabetically sorted into the list.
<br><b>To remove players:</b> clear out their entries in the list.</p>
<p>Player name matching is case insensitive. Immediately after a watched player begins joining the server, an email is dispatched to the destination address. <br>The default GMail SMTP server is <i>smtp.gmail.com</i> and port number is <i>587</i> with <i>SSL enabled.</i></p>";
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
                this.toConsole(3, soldierName + " just joined...");
                string lowerName = soldierName.ToLower();
                if (watchlist.Contains(lowerName))
                {
					watchedPlayerJoined(soldierName);
                }
				else
				{
                    if (watchlistFilepath.Length > 0)
                    {
                        this.toConsole(3, "Watchlist file last checked " + watchlistTime.ToString("G"));
                        if (DateTime.Now.Subtract(watchlistTime).TotalMinutes > 3) //is the cache older than 3 minutes?
                        {
                            watchlistTime = DateTime.Now; //prevent any new threads for being created during the check
                            new Thread((ThreadStart)delegate { checkWatchlistFile(soldierName); }).Start(); //check on a separate thread
                        }
                        else
                        {
                            checkWatchlistCache(soldierName); //check on the same thread
                        }
                    }
				}
            }
        }

		private void checkWatchlistFile(string soldierName)
		{
            this.toConsole(2, "Watchlist file contents out of date, refreshing...");
            try
            {
                watchlistLines = System.IO.File.ReadAllLines(watchlistFilepath);
            }
            catch (Exception e)
            {
                this.toConsole(1, "File read error: " + e);
            }
            checkWatchlistCache(soldierName);
		}

        public void checkWatchlistCache(string soldierName)
        {
            string lowerName = soldierName.ToLower();
            foreach (string line in watchlistLines)
            {
                if (line.Trim().Equals(lowerName))
                {
                    watchedPlayerJoined(soldierName);
                    break;
                }
            }
        }

		private void watchedPlayerJoined(string soldierName)
		{
			this.toConsole(2, "Alert!");
			this.toConsole(1, String.Format("{0}, who is a watched player, just joined the server.", soldierName));
			this.sendOutEmail(soldierName);
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
                    System.Net.NetworkCredential basicAuthenticationInfo = new System.Net.NetworkCredential(this.SMTPUsername, this.SMTPPassword);
                    mySmtpClient.Credentials = basicAuthenticationInfo;
                    mySmtpClient.Port = this.SMTPPort;
                    if (this.ssl == enumBoolYesNo.Yes)
                    {
                        mySmtpClient.EnableSsl = true;
                    }
                    else
                    {
                        mySmtpClient.EnableSsl = false;
                    }

                    MailAddress from = new MailAddress("bf-development@purebattlefield.org", "WatchDog on " + serverName);
                    MailAddress to = new MailAddress(this.emailAddress, "Destination");
                    MailMessage myMail = new System.Net.Mail.MailMessage(from, to);

                    myMail.Subject = String.Format("WatchDog Alert: Player {0} just joined {1}", offendor, this.serverName);
                    myMail.SubjectEncoding = System.Text.Encoding.UTF8;

                    myMail.Body = String.Format("<p>{0} : {1}, who is a watched player, just joined {2}.</p> {3}",
                        DateTime.Now.ToString("G"),
                        offendor,
                        this.serverName,
                        "<p><i>This is an automated email sent by WatchDog for PRoCon. The watched player list can be configured in the plugin settings.</i></p>");
                    myMail.BodyEncoding = System.Text.Encoding.UTF8;
                    myMail.IsBodyHtml = true;

                    mySmtpClient.Send(myMail);
                    this.toConsole(1, "An email message was dispatched regarding " + offendor + ".");
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

			lstReturn.Add(new CPluginVariable("Player Watchlist|(Optional) Watchlist file", typeof(string), watchlistFilepath));

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
            lstReturn.Add(new CPluginVariable("Email/SMTP Settings|SMTP Port", typeof(string), this.SMTPPort.ToString()));
            lstReturn.Add(new CPluginVariable("Email/SMTP Settings|SMTP Username", typeof(string), this.SMTPUsername));
            lstReturn.Add(new CPluginVariable("Email/SMTP Settings|SMTP Password", typeof(string), this.SMTPPassword));
            lstReturn.Add(new CPluginVariable("Email/SMTP Settings|Enable SSL?", typeof(enumBoolYesNo), this.ssl));
            lstReturn.Add(new CPluginVariable("Email/SMTP Settings|Destination Email Address", typeof(string), this.emailAddress));
            lstReturn.Add(new CPluginVariable("Other Settings|Server Shortname", typeof(string), this.serverName));
            lstReturn.Add(new CPluginVariable("Other Settings|Send a test message (type anything)", typeof(string), ""));
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
				if (strVariable.Contains("Watchlist file"))
				{
                    //string tempFilePath = watchlistFilepath;
                    watchlistFilepath = strValue.Trim().Replace('\\','/');
                    if (watchlistFilepath.Length > 0)
                    {
                        try
                        {
                            watchlistLines = System.IO.File.ReadAllLines(watchlistFilepath);
                            watchlistTime = DateTime.Now;
                            this.toConsole(1, "File read OK! Path: " + watchlistFilepath);
                        }
                        catch (Exception e)
                        {
                            this.toConsole(1, "File read error: " + e);
                        }
                    }
				}
				else if (strVariable.Contains("Soldier name:"))
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
				else if (strVariable.Contains("Enable SSL?") && Enum.IsDefined(typeof(enumBoolYesNo), strValue) == true)
				{
					this.ssl = (enumBoolYesNo)Enum.Parse(typeof(enumBoolYesNo), strValue);
					if (this.ssl == enumBoolYesNo.Yes)
						this.toConsole(2, "SSL enabled.");
					else
						this.toConsole(2, "SSL disabled.");
				}
				else if (strVariable.Contains("SMTP Server"))
				{
					this.SMTPServer = strValue.Trim();
				}
				else if (strVariable.Contains("SMTP Port"))
				{
					try
					{
						this.SMTPPort = Int32.Parse(strValue.Trim());
					}
					catch (Exception z)
					{
						this.toConsole(1, "Invalid port value! Use integer values only.");
						this.SMTPPort = 465;
					}
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
				else if (strVariable.Contains("Server Shortname"))
				{
					this.serverName = strValue.Trim();
				}
				else if (strVariable.Contains("Send a test message (type anything)"))
				{
                    if(!String.IsNullOrEmpty(strValue.Trim()))
					    this.sendOutEmail("Test");
				}
				else if (strVariable.Contains("Debug Level"))
				{
					try
					{
						this.debugLevel = Int32.Parse(strValue.Trim());
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