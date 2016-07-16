﻿using GameServer.Config;
using GameServer.Network;
using NLog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace GameServer.Service.Commands
{
    public static class CommandManager
    {
        /// <summary>
        /// Logger for this class
        /// </summary>
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        /// <summary>
        /// 
        /// </summary>
        private static readonly Dictionary<CommandGroupAttribute, CommandGroup> CommandGroups = new Dictionary<CommandGroupAttribute, CommandGroup>();

        /// <summary>
        /// 
        /// </summary>
        static CommandManager()
        {
            RegisterCommandGroups();
        }

        /// <summary>
        /// 
        /// </summary>
        private static void RegisterCommandGroups()
        {
            foreach (var type in Assembly.GetExecutingAssembly().GetTypes())
            {
                if (!type.IsSubclassOf(typeof(CommandGroup))) continue;

                var attributes = (CommandGroupAttribute[])type.GetCustomAttributes(typeof(CommandGroupAttribute), true);
                if (attributes.Length == 0) continue;

                var groupAttribute = attributes[0];
                if (CommandGroups.ContainsKey(groupAttribute))
                    Logger.Warn("There exists an already registered command group named '{0}'.", groupAttribute.Name);

                var commandGroup = (CommandGroup)Activator.CreateInstance(type);
                commandGroup.Register(groupAttribute);
                CommandGroups.Add(groupAttribute, commandGroup);
            }
        }

        /// <summary>
        /// Parses a given line from console as a command if any.
        /// </summary>
        /// <param name="line">The line to be parsed.</param>
        public static void Parse(string line)
        {
            string output = string.Empty;
            string command;
            string parameters;
            var found = false;

            if (line == null) return;
            if (line.Trim() == string.Empty) return;

            if (!ExtractCommandAndParameters(line, out command, out parameters))
            {
                output = "Unknown command: " + line;
                Logger.Info(output);
                return;
            }

            foreach (var pair in CommandGroups)
            {
                if (pair.Key.Name != command) continue;
                output = pair.Value.Handle(parameters);
                found = true;
                break;
            }

            if (found == false)
                output = string.Format("Unknown command: {0} {1}", command, parameters);

            if (output != string.Empty)
                Logger.Info(output);
        }


        /// <summary>
        /// Tries to parse given line as a server command.
        /// </summary>
        /// <param name="line">The line to be parsed.</param>
        /// <param name="invokerClient">The invoker client if any.</param>
        /// <returns><see cref="bool"/></returns>
        public static bool TryParse(string line, Connection invokerCon)
        {
            string output = string.Empty;
            string command;
            string parameters;
            var found = false;

            if (invokerCon == null)
                throw new ArgumentException("invokerCon");

            if (!ExtractCommandAndParameters(line, out command, out parameters))
                return false;

            foreach (var pair in CommandGroups)
            {
                if (pair.Key.Name != command) continue;
                output = pair.Value.Handle(parameters, invokerCon);
                found = true;
                break;
            }

            if (found == false)
                output = string.Format("Unknown command: {0} {1}", command, parameters);

            if (output == string.Empty)
                return true;

            output = "[TEST] " + output;

            //invokerCon.SendServerWhisper(output);

            return true;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="line"></param>
        /// <param name="command"></param>
        /// <param name="parameters"></param>
        /// <returns></returns>
        public static bool ExtractCommandAndParameters(string line, out string command, out string parameters)
        {
            line = line.Trim();
            command = string.Empty;
            parameters = string.Empty;

            if (line == string.Empty)
                return false;

            if (line[0] != CommandConfig.Instance.CommandPrefix) // if line does not start with command-prefix
                return false;

            line = line.Substring(1); // advance to actual command.
            command = line.Split(' ')[0].ToLower(); // get command
            parameters = String.Empty;
            if (line.Contains(' ')) parameters = line.Substring(line.IndexOf(' ') + 1).Trim(); // get parameters if any.

            return true;
        }

        /// <summary>
        /// 
        /// </summary>
        [CommandGroup("commands", "Lists available commands for your user-level.")]
        public class CommandsCommandGroup : CommandGroup
        {
            public override string Fallback(string[] parameters = null, Connection invokerCon = null)
            {
                var output = "Available commands: ";
                foreach (var pair in CommandGroups)
                {
                    if (invokerCon != null && pair.Key.MinUserLevel > invokerCon.Account.AccountLevel) continue;
                    output += pair.Key.Name + ", ";
                }

                output = output.Substring(0, output.Length - 2) + ".";
                return output + "\nType 'help <command>' to get help.";
            }
        }

        /// <summary>
        /// 
        /// </summary>
        [CommandGroup("help", "Oh no, we forgot to add a help to text to help command itself!")]
        public class HelpCommandGroup : CommandGroup
        {
            public override string Fallback(string[] parameters = null, Connection invokerCon = null)
            {
                return "usage: help <command>";
            }

            public override string Handle(string parameters, Connection invokerCon = null)
            {
                if (parameters == string.Empty)
                    return this.Fallback();

                string output = string.Empty;
                bool found = false;
                var @params = parameters.Split(' ');
                var group = @params[0];
                var command = @params.Count() > 1 ? @params[1] : string.Empty;

                foreach (var pair in CommandGroups)
                {
                    if (group != pair.Key.Name)
                        continue;

                    if (command == string.Empty)
                        return pair.Key.Help;

                    output = pair.Value.GetHelp(command);
                    found = true;
                }

                if (!found)
                    output = string.Format("Unknown command: {0} {1}", group, command);

                return output;
            }
        }
    }
}
