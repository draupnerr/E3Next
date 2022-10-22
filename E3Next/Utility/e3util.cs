﻿using E3Core.Data;
using E3Core.Processors;
using IniParser;
using MonoCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using static MonoCore.EventProcessor;

namespace E3Core.Utility
{
    public static class e3util
    {

        public static string _lastSuccesfulCast = String.Empty;
        public static Logging _log = E3._log;
        private static IMQ MQ = E3.MQ;
        private static ISpawns _spawns = E3._spawns;
        /// <summary>
        /// Use to see if a certain method should be running
        /// </summary>
        /// <param name="nextCheck">ref param to update ot the next time a thing should run</param>
        /// <param name="nextCheckInterval">The interval in milliseconds</param>
        /// <returns></returns>
        public static bool ShouldCheck(ref Int64 nextCheck, Int64 nextCheckInterval)
        {  
            if (Core._stopWatch.ElapsedMilliseconds < nextCheck)
            {
                return false;
            }
            else
            {
                nextCheck = Core._stopWatch.ElapsedMilliseconds + nextCheckInterval;
                return true;
            }
        }

        public static void TryMoveToTarget()
        {
            Double meX = MQ.Query<Double>("${Me.X}");
            Double meY = MQ.Query<Double>("${Me.Y}");

            Double x = MQ.Query<Double>("${Target.X}");
            Double y = MQ.Query<Double>("${Target.Y}");
            MQ.Cmd($"/squelch /moveto loc {y} {x}");
            MQ.Delay(500);

            Int64 endTime = Core._stopWatch.ElapsedMilliseconds + 10000;
            while(true)
            {
               
                Double tmeX = MQ.Query<Double>("${Me.X}");
                Double tmeY = MQ.Query<Double>("${Me.Y}");

                if((int)meX==(int)tmeX && (int)meY==(int)tmeY)
                {
                    //we are stuck, kick out
                    break;
                }

                meX = tmeX;
                meY = tmeY;

                if (endTime < Core._stopWatch.ElapsedMilliseconds)
                {
                    break;
                }
                MQ.Delay(200);
            }

        }

        public static bool FilterMe(CommandMatch x)
        {
            ////Stop /Only|Soandoso
            ////FollowOn /Only|Healers WIZ Soandoso
            ////followon /Not|Healers /Exclude|Uberhealer1
            /////Staunch /Only|Healers
            /////Follow /Not|MNK
            //things like this put into the filter collection.
            //process the filters for commands
            bool returnValue = false;

            //get any 'only' filter.
            //get any 'include/exclude' filter with it.
            string onlyFilter = string.Empty;
            string notFilter = String.Empty;
            string excludeFilter = string.Empty;
            string includeFilter = String.Empty;
            foreach (var filter in x.filters)
            {
                if (filter.StartsWith("/only", StringComparison.OrdinalIgnoreCase))
                {
                    onlyFilter = filter;
                }
                if (filter.StartsWith("/not", StringComparison.OrdinalIgnoreCase))
                {
                    notFilter = filter;
                }
                if (filter.StartsWith("/exclude", StringComparison.OrdinalIgnoreCase))
                {
                    excludeFilter = filter;
                }
                if (filter.StartsWith("/include", StringComparison.OrdinalIgnoreCase))
                {
                    includeFilter = filter;
                }
            }

            List<string> includeInputs = new List<string>();
            List<string> excludeInputs = new List<string>();
            //get the include/exclude values first before we process /not/only

            if (onlyFilter != string.Empty)
            {
                //assume we are excluded unless we match with an only filter
                returnValue = true;

                Int32 indexOfPipe = onlyFilter.IndexOf('|') + 1;
                string input = onlyFilter.Substring(indexOfPipe, onlyFilter.Length - indexOfPipe);
                //now split up into a list of values.
                List<string> inputs = StringsToList(input, ' ');

                if (!FilterReturnCheck(inputs, ref returnValue, false))
                {
                    return false;
                }
               
                if (includeFilter != String.Empty)
                {
                    indexOfPipe = includeFilter.IndexOf('|') + 1;
                    string icludeInput = includeFilter.Substring(indexOfPipe, includeFilter.Length - indexOfPipe);
                    includeInputs = StringsToList(icludeInput, ' ');

                    if (!FilterReturnCheck(includeInputs, ref returnValue, false))
                    {
                        return false;
                    }
                }
                if (excludeFilter != String.Empty)
                {
                    indexOfPipe = excludeFilter.IndexOf('|') + 1;
                    string excludeInput = excludeFilter.Substring(indexOfPipe, excludeFilter.Length - indexOfPipe);
                    excludeInputs = StringsToList(excludeInput, ' ');

                    if (FilterReturnCheck(excludeInputs, ref returnValue, true))
                    {
                        return true;
                    }
                }

            }
            else if (notFilter != string.Empty)
            {
                returnValue = false;
                 Int32 indexOfPipe = notFilter.IndexOf('|') + 1;
                string input = notFilter.Substring(indexOfPipe, notFilter.Length - indexOfPipe);
                //now split up into a list of values.
                List<string> inputs = StringsToList(input, ' ');

                if (FilterReturnCheck(inputs, ref returnValue, true))
                {
                    return true;
                }

                if (includeFilter != String.Empty)
                {
                    indexOfPipe = includeFilter.IndexOf('|') + 1;
                    string icludeInput = includeFilter.Substring(indexOfPipe, includeFilter.Length - indexOfPipe);
                    includeInputs = StringsToList(icludeInput, ' ');

                    if (!FilterReturnCheck(includeInputs, ref returnValue, false))
                    {
                        return false;
                    }
                }
                if (excludeFilter != String.Empty)
                {
                    indexOfPipe = excludeFilter.IndexOf('|') + 1;
                    string excludeInput = excludeFilter.Substring(indexOfPipe, excludeFilter.Length - indexOfPipe);
                    excludeInputs = StringsToList(excludeInput, ' ');

                    if (FilterReturnCheck(excludeInputs, ref returnValue, true))
                    {
                        return true;
                    }
                }
            }


            return returnValue;
        }

        private static bool FilterReturnCheck(List<string> inputs, ref bool returnValue, bool inputSetValue)
        {
            if (inputs.Contains(E3._currentName, StringComparer.OrdinalIgnoreCase))
            {
                return inputSetValue;
            }
            if (inputs.Contains(E3._currentShortClassString, StringComparer.OrdinalIgnoreCase))
            {
                returnValue = inputSetValue;
            }
            if (inputs.Contains("Healers", StringComparer.OrdinalIgnoreCase))
            {
                if ((E3._currentClass & Class.Priest) == E3._currentClass)
                {
                    returnValue = inputSetValue;
                }
            }
            if (inputs.Contains("Tanks", StringComparer.OrdinalIgnoreCase))
            {
                if ((E3._currentClass & Class.Tank) == E3._currentClass)
                {
                    returnValue = inputSetValue;
                }
            }
            if (inputs.Contains("Melee", StringComparer.OrdinalIgnoreCase))
            {
                if ((E3._currentClass & Class.Melee) == E3._currentClass)
                {
                    returnValue = inputSetValue;
                }
            }
            if (inputs.Contains("Casters", StringComparer.OrdinalIgnoreCase))
            {
                if ((E3._currentClass & Class.Caster) == E3._currentClass)
                {
                    returnValue = inputSetValue;
                }
            }
            if (inputs.Contains("Ranged", StringComparer.OrdinalIgnoreCase))
            {
                if ((E3._currentClass & Class.Ranged) == E3._currentClass)
                {
                    returnValue = inputSetValue;
                }
            }

            return !inputSetValue;
        }

        public static void StringsToNumbers(string s, char delim, List<Int32> list)
        {
            List<int> result = list;
            int start = 0;
            int end = 0;
            foreach (char x in s)
            {
                if (x == delim || end == s.Length - 1)
                {
                    if (end == s.Length - 1)
                        end++;
                    result.Add(int.Parse(s.Substring(start, end - start)));
                    start = end + 1;
                }
                end++;
            }

        }
        public static List<string> StringsToList(string s, char delim)
        {
            List<string> result = new List<string>();
            int start = 0;
            int end = 0;
            foreach (char x in s)
            {
                if (x == delim || end == s.Length - 1)
                {
                    if (end == s.Length - 1)
                        end++;
                    result.Add((s.Substring(start, end - start)));
                    start = end + 1;
                }
                end++;
            }

            return result;
        }
        public static void TryMoveToLoc(Double x, Double y,Int32 minDistance = 0,Int32 timeoutInMS = 10000 )
        {
            Double meX = MQ.Query<Double>("${Me.X}");
            Double meY = MQ.Query<Double>("${Me.Y}");
            MQ.Cmd($"/squelch /moveto loc {y} {x} mdist {minDistance}");
            if (timeoutInMS == -1) return;
            Int64 endTime = Core._stopWatch.ElapsedMilliseconds + timeoutInMS;
            MQ.Delay(300);
            while (true)
            {
                Double tmeX = MQ.Query<Double>("${Me.X}");
                Double tmeY = MQ.Query<Double>("${Me.Y}");

                if ((int)meX == (int)tmeX && (int)meY == (int)tmeY)
                {
                    //we are stuck, kick out
                    break;
                }

                meX = tmeX;
                meY = tmeY;

                if (endTime < Core._stopWatch.ElapsedMilliseconds)
                {
                    MQ.Cmd($"/squelch /moveto off");
                    break;
                }

                MQ.Delay(200);
            }


        }

        public static void PrintTimerStatus(Dictionary<Int32, SpellTimer> timers, ref Int64 printTimer, string Caption, Int64 delayInMS = 10000)
        {
            //Printing out debuff timers
            if (printTimer < Core._stopWatch.ElapsedMilliseconds)
            {
                if (timers.Count > 0)
                {
                    MQ.Write($"\at{Caption}");
                    MQ.Write("\aw===================");


                }

                foreach (var kvp in timers)
                {
                    foreach (var kvp2 in kvp.Value._timestamps)
                    {
                        Data.Spell spell;
                        if (Spell._loadedSpells.TryGetValue(kvp2.Key, out spell))
                        {
                            Spawn s;
                            if (_spawns.TryByID(kvp.Value._mobID, out s))
                            {
                                MQ.Write($"\ap{s.CleanName} \aw: \ag{spell.CastName} \aw: {(kvp2.Value - Core._stopWatch.ElapsedMilliseconds) / 1000} seconds");

                            }

                        }
                        else
                        {
                            Spawn s;
                            if (_spawns.TryByID(kvp.Value._mobID, out s))
                            {
                                MQ.Write($"\ap{s.CleanName} \aw: \agspellid:{kvp2.Key} \aw: {(kvp2.Value - Core._stopWatch.ElapsedMilliseconds) / 1000} seconds");

                            }

                        }

                    }
                }
                if (timers.Count > 0)
                {
                    MQ.Write("\aw===================");

                }
                printTimer = Core._stopWatch.ElapsedMilliseconds + delayInMS;

            }
        }
        public static void RegisterCommandWithTarget(string command, Action<int> FunctionToExecute)
        {
            EventProcessor.RegisterCommand(command, (x) =>
            {
                 Int32 mobid;
                if (x.args.Count > 0)
                {
                    if (Int32.TryParse(x.args[0], out mobid))
                    {
                        FunctionToExecute(mobid);
                    }
                    else
                    {
                        MQ.Broadcast($"\aNeed a valid target to {command}.");
                    }
                }
                else
                {
                    Int32 targetID = MQ.Query<Int32>("${Target.ID}");
                    if (targetID > 0)
                    {
                        //we are telling people to follow us
                        E3._bots.BroadcastCommandToGroup($"{command} {targetID}");
                        FunctionToExecute(targetID);
                    }
                    else
                    {
                        MQ.Write($"\aNEED A TARGET TO {command}");
                    }
                }
            });

        }
        public static FileIniDataParser CreateIniParser()
        {
            var fileIniData = new FileIniDataParser();
            fileIniData.Parser.Configuration.AllowDuplicateKeys = true;
            fileIniData.Parser.Configuration.OverrideDuplicateKeys = true;// so that the other ones will be put into a collection
            fileIniData.Parser.Configuration.AssigmentSpacer = "";
            fileIniData.Parser.Configuration.CaseInsensitive = true;
           
            return fileIniData;
        }

      

    }
}
