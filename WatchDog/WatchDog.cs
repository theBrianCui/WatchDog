//Import various C# things.
using System;
using System.IO;
using System.Text;
using System.Reflection;
using System.Collections.Generic;
using System.Data;
using System.Text.RegularExpressions;

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
        private String someString = "string";
        private int debugLevel = 1;

        public WatchDog()
        {

        }

        public string GetPluginName()
        {
            return "WatchDog";
        }

        public string GetPluginVersion()
        {
            return "0.0.0";
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
            return @"A basic plugin template. Does nothing.";
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
                this.ExecuteCommand("procon.protected.pluginconsole.write", message);
            }
        }

        public void OnPluginLoaded(string strHostName, string strPort, string strPRoConVersion)
        {
            this.RegisterEvents(this.GetType().Name, "OnPluginLoaded");
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
            lstReturn.Add(new CPluginVariable("Settings|Some String", typeof(string), this.someString));
            lstReturn.Add(new CPluginVariable("Settings|Debug Level", typeof(string), this.debugLevel.ToString()));
            return lstReturn;
        }

        public List<CPluginVariable> GetPluginVariables()
        {
            return GetDisplayPluginVariables();
        }

        public void SetPluginVariable(String strVariable, String strValue)
        {
            if (strVariable.Contains("Some String"))
            {
                this.someString = strValue;
            }
            else if (strVariable.Contains("Debug Level"))
            {
                try
                {
                    this.debugLevel = Int32.Parse(strValue);
                }
                catch (Exception z)
                {
                    this.toConsole(1, "Invalid debug level! Choose 0, 1, or 2 only.");
                    this.debugLevel = 1;
                }
            }
        }
    }
}