using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using System.Diagnostics;

namespace MLatBPM_DataPreprocessing
{
    class Program
    {
        // Add variables for the tags inside the XES file. 
        public const string tag_trace = "trace";  // For RTFM is "trace" for all the other logs is "{http://www.xes-standard.org/}trace"
        public const string tag_event = "event";  // For RTFM is "event" for all the other logs is "{http://www.xes-standard.org/}event"
        static void Main(string[] args)
        {
            int numberOfEvents = 6; //Number of events included in data. It goes from 1 up to ... BPIC11: 10, BPIC13: 10, RTFM: 6, RL: 10.
            string keyTraceIdentifier = "concept:name"; //Trace Identifier needs to be removed from data because it is not valid to train on an ID

            //#######################################################################################
            //relevant variables to be defined for each log individually
            //#######################################################################################
            // BPIC11
            //XDocument doc = XDocument.Load("..\\..\\..\\..\\..\\data\\original\\BPIC11.xes");
            //string outputPathInclFileName = "..\\..\\..\\..\\..\\data\\transformed\\bpic11\\bpic11_transformed_"+numberOfEvents+".csv";
            // BPIC13
            //XDocument doc = XDocument.Load("..\\..\\..\\..\\..\\data\\original\\BPIC13.xes");
            //string outputPathInclFileName = "..\\..\\..\\..\\..\\data\\transformed\\bpic13\\bpic13_transformed_"+numberOfEvents+".csv";
            // RTFM
            XDocument doc = XDocument.Load("..\\..\\..\\..\\..\\data\\original\\RTFM.xes");
            string outputPathInclFileName = "..\\..\\..\\..\\..\\data\\transformed\\rtfm\\rtfm_transformed_"+numberOfEvents+".csv";
            // RL
            //XDocument doc = XDocument.Load("..\\..\\..\\..\\..\\data\\original\\RL.xes");
            //string outputPathInclFileName = "..\\..\\..\\..\\..\\data\\transformed\\rl\\rl_transformed_"+numberOfEvents+".csv";

            var root = doc.Root;
            var traces = root.Elements(tag_trace);

            //labelingHospital(traces); // For bpic11
            //labelingIncidentMgmt(traces);  // For bpic13
            labelingRTFM(traces); // For RTFM
            //labelingRL(traces); // For RL


            //#######################################################################################
            //check if event variables need to be transformed to trace variables
            //if a variable is included in every event and the value is consistent for each trace, 
            //then the variable can be transformed to a trace variable 
            //#######################################################################################
            List<string> eventVarsKeys = new List<string>();
            var eventVarsFirstTrace = traces.ElementAt(0).Elements(tag_event).Elements();

            foreach(var trace in traces)
            {
                foreach(string evVar in eventVarsKeys)
                {
                    var eventsInTrace = trace.Elements(tag_event);
                    var varInFirstEvent = eventsInTrace.ElementAt(0).Elements().Where(x => (string)x.Attribute("key") == evVar).First();

                    
                    if(varInFirstEvent == null)
                    {
                        eventVarsKeys.Remove(evVar);
                        continue;
                    }

                    string varInFirstEventValue = varInFirstEvent.Attribute("value").Value;
                    foreach(var ev in eventsInTrace)
                    {
                        var varInEvent = ev.Elements().Where(x => (string)x.Attribute("key") == evVar).First();

                        if(varInEvent == null)
                        {
                            eventVarsKeys.Remove(evVar);
                            break;
                        }
                        if(varInEvent.Attribute("value").Value != varInFirstEventValue)
                        {
                            eventVarsKeys.Remove(evVar);
                            break;
                        }
                    }
                }
            }

            //list eventVarsKeys now only contains variable names that are currently listed as event variables, but could be transformed to trace variables
            foreach(var trace in traces)
            {
                foreach (string evVar in eventVarsKeys)
                {
                    trace.Add(trace.Elements(tag_event).ElementAt(0).Elements().Where(x => (string)x.Attribute("key") == evVar).First());
                }
            }
            foreach(string evVar in eventVarsKeys)
            {
                traces.Elements(tag_event).Elements().Where(x => (string)x.Attribute("key") == evVar).Remove();
            }


            //#######################################################################################
            //only include traces that have a sufficient number of events 
            //for traces with a sufficient number of events, 
            //remove events that exceed the number of events that are supposed to be included in the log
            //#######################################################################################
            var tracesToRemove = from trace in traces where trace.Elements(tag_event).Count() <= numberOfEvents select trace;
            tracesToRemove.Remove();

            foreach (var trace in traces)
            {
                while (trace.Elements(tag_event).Count() > numberOfEvents)
                {
                    trace.Elements(tag_event).Last().Remove();
                }
            }

            //remove trace key attribute from trace
            traces.Elements().Where(x => (string)x.Attribute("key") == keyTraceIdentifier).Remove();

            //#######################################################################################
            //get all trace variables and check, which ones need to be included in the dataset. 
            //if they only occur in less than 1% of the traces, the variables are removed.
            //#######################################################################################
            List<string> traceVariableNames = new List<string>();

            var traceVariables = traces.Elements().Where(x => (string)x.Name.LocalName != "event"); // this should be "event" instead of "tag_event"
            foreach(var traceVariable in traceVariables)
            {
                traceVariableNames.Add(traceVariable.Attribute("key").Value);
            }
            
            int numberOfTraces = traces.Count();

            List<string> traceVariablesExcludedFromLog = new List<string>();
            List<string> traceVariablesIncludedInLog = new List<string>();

            var g = traceVariableNames.GroupBy(x => x);

            foreach (var grp in g)
            {
                if(((double)grp.Count() / numberOfTraces) <= 0.01)
                {
                    traceVariablesExcludedFromLog.Add(grp.Key);
                }
                else
                {
                    traceVariablesIncludedInLog.Add(grp.Key);
                }
            }

            //remove identified trace attributes from traces
            foreach (string traceVariableToExclude in traceVariablesExcludedFromLog)
            {
                traces.Elements().Where(x => (string)x.Attribute("key") == traceVariableToExclude).Remove();
            }


            //get all variables from relevant events and check if they need to be included in the data set
            //if they only occur in less than 1% of the traces, the variables are removed.
            //this procedure is done for each event individually. Meaning, in each iteration, only the i_th event in each trace is checked.

            List<string>[] eventVariablesExcludedFromLog = new List<string>[numberOfEvents];
            List<string>[] eventVariablesIncludedInLog = new List<string>[numberOfEvents];

            for (int i = 0; i < numberOfEvents; i++)
            {
                eventVariablesExcludedFromLog[i] = new List<string>();
                eventVariablesIncludedInLog[i] = new List<string>();

                List<string> eventVariableNames = new List<string>();

                //get all attributes for event i in all traces
                foreach (var trace in traces)
                {
                    var attr = trace.Elements(tag_event).ElementAt(i).Elements();

                    foreach(var a in attr)
                    {
                        eventVariableNames.Add(a.Attribute("key").Value);
                    }
                }

                //check which attributes need to be removed from events i in traces
                var groupedEvents = eventVariableNames.GroupBy(x => x);

                foreach (var grp in groupedEvents)
                {
                    if (((double)grp.Count() / numberOfTraces) <= 0.01)
                    {
                        eventVariablesExcludedFromLog[i].Add(grp.Key);
                    }
                    else
                    {
                        eventVariablesIncludedInLog[i].Add(grp.Key);
                    }
                }

                //remove identified attributes from events i in traces
                foreach (var trace in traces)
                {
                    var attr = trace.Elements(tag_event).ElementAt(i).Elements();

                    foreach (var variableExcluded in eventVariablesExcludedFromLog[i])
                    {
                        attr.Where(x => (string)x.Attribute("key") == variableExcluded).Remove();
                    }
                }
            }


            //#######################################################################################
            //translate strings in dataset to integers
            //#######################################################################################
            List<Tuple<string, string[]>> dictTrace = new List<Tuple<string, string[]>>();
            foreach (string traceVar in traceVariablesIncludedInLog)
            {
                //get datatype of trace Var
                var traceVarElements = traces.Elements().Where(x => (string)x.Attribute("key") == traceVar);
                bool bDictRequired = false;

                foreach (var traceVarElement in traceVarElements)
                {
                    if(traceVarElement.Name.LocalName == "string")
                    {
                        bDictRequired = true;
                        break;
                    }
                }
                if (bDictRequired)
                {
                    List<string> traceVarElementsValues = new List<string>();

                    foreach (var traceVarElement in traceVarElements)
                    {
                        traceVarElementsValues.Add(traceVarElement.Attribute("value").Value);
                    }

                    dictTrace.Add(new Tuple<string, string[]>(traceVar, traceVarElementsValues.Distinct().ToArray()));
                }
            }

            List<Tuple<string, string[]>> dictEvent = new List<Tuple<string, string[]>>();
            List<string> totalEventVariablesIncludedInLog = new List<string>();
            for(int i = 0; i < numberOfEvents; i++)
            {
                totalEventVariablesIncludedInLog.AddRange(eventVariablesIncludedInLog[i]);
            }
            totalEventVariablesIncludedInLog = totalEventVariablesIncludedInLog.Distinct().ToList();

            foreach(string eventVar in totalEventVariablesIncludedInLog)
                {
                //get datatype of trace Var
                var eventVarElements = traces.Elements(tag_event).Elements().Where(x => (string)x.Attribute("key") == eventVar);
                bool bDictRequired = false;

                foreach (var eventVarElement in eventVarElements)
                {
                    if (eventVarElement.Name.LocalName == "string")
                    {
                        bDictRequired = true;
                        break;
                    }
                }
                if (bDictRequired)
                {
                    List<string> eventVarElementsValues = new List<string>();

                    foreach (var eventVarElement in eventVarElements)
                    {
                        eventVarElementsValues.Add(eventVarElement.Attribute("value").Value);
                    }

                    dictEvent.Add(new Tuple<string, string[]>(eventVar, eventVarElementsValues.Distinct().ToArray()));
                }
            }

            //#######################################################################################
            //Convert date attributes to integers
            //#######################################################################################
            var dateTranslation = traces.Descendants().Where(x => x.Name.LocalName == "date");

            foreach (var dateTr in dateTranslation)
            {
                string val = dateTr.Attribute("value").Value;

                val = val.Substring(0, 19).Replace('T', ' ');
                DateTime myDate = DateTime.ParseExact(val, "yyyy-MM-dd HH:mm:ss",
                           System.Globalization.CultureInfo.InvariantCulture);
          
                dateTr.Attribute("value").Value = myDate.Ticks.ToString();
            }

            //#######################################################################################
            //Generate output array
            //#######################################################################################
            List<string> output = new List<string>();

            foreach(var trace in traces)
            {
                List<string> outputCurTrace = new List<string>();
                foreach(string traceAttr in traceVariablesIncludedInLog)
                {
                    string val = "0";
                    var elems = trace.Elements().Where(x => (string)x.Attribute("key") == traceAttr);

                    if(elems.Count() > 0)
                    {
                        val = elems.First().Attribute("value").Value;

                        if(dictTrace.Find(x => x.Item1 == traceAttr) != null)
                        {
                            string[] curDictTrace = dictTrace.Find(x => x.Item1 == traceAttr).Item2;

                            int indexInDict = Array.FindIndex(curDictTrace, x => x == val);

                            val = (indexInDict + 1).ToString();
                        }
                    }

                    outputCurTrace.Add(val);
                }

                for(int i = 0; i < numberOfEvents; i++)
                {
                    var curEvent = trace.Elements(tag_event).ElementAt(i);
                
                    foreach(string eventAttribute in eventVariablesIncludedInLog[i])
                    {
                        string val = "0";
                        var elems = curEvent.Elements().Where(x => (string) x.Attribute("key") == eventAttribute);

                        if (elems.Count() > 0)
                        {
                            val = elems.First().Attribute("value").Value;

                            if (dictEvent.Find(x => x.Item1 == eventAttribute) != null)
                            {
                                string[] curDictEvent = dictEvent.Find(x => x.Item1 == eventAttribute).Item2;

                                int indexInDict = Array.FindIndex(curDictEvent, x => x == val);

                                val = (indexInDict + 1).ToString();
                            }
                        }

                        outputCurTrace.Add(val);
                    }
                }

                //add Label
                outputCurTrace.Add(trace.Attribute("Label").Value);

                output.Add(String.Join(";", outputCurTrace));
            }

            System.IO.File.WriteAllLines(outputPathInclFileName, output.ToArray());

            Console.ReadKey();
        }

        public static void labelingHospital(IEnumerable<XElement> traces)
        {
            //Labeling Hospital: 
            //If Name of any event in trace contains "spoed" (= "urgent") mark as urgent
            foreach (var trace in traces)
            {
                int iTraceIsUrgent = 2;

                var elems_EventName = trace.Elements(tag_event).Elements().Where(x => (string)x.Attribute("key") == "concept:name");

                foreach (var el in elems_EventName)
                {
                    if (el.Attribute("value").Value.Contains("spoed"))
                    {
                        iTraceIsUrgent = 1;
                        break;
                    }
                }
                trace.SetAttributeValue("Label", iTraceIsUrgent.ToString());
            }
        }

        public static void labelingIncidentMgmt(IEnumerable<XElement> traces)
        {
            //Labeling Incident Management: 
            //Labeling depends on the highest support lane, that was involved in the solution of the incident.
            //The support lane level is encoded in the org:group. If it only contains a name, it is support level 1. If it contains "2nd", it is support level 2. If it contains "3rd", it is support level 3.
            foreach (var trace in traces)
            {
                int i_LabelIncident = 1;

                var elems_EventName = trace.Elements(tag_event).Elements().Where(x => (string)x.Attribute("key") == "org:group");

                foreach (var el in elems_EventName)
                {
                    if (el.Attribute("value").Value.Contains("3rd"))
                    {
                        i_LabelIncident = 3;
                        break;
                    }
                    else if (el.Attribute("value").Value.Contains("2nd"))
                    {
                        i_LabelIncident = 2;
                    }
                }

                trace.SetAttributeValue("Label", i_LabelIncident.ToString());
            }
        }

        public static void labelingRTFM(IEnumerable<XElement> traces)
        {
            //Labeling RTFM:
            //Was Judge involved in dismissal of case?
            //If Event with concept:name = "Appeal to Judge" is included, then judge was involved. Otherwise, no judge was involved
            foreach (var trace in traces)
            {
                int iJudgeInvolved = 2;

                var elems_EventName = trace.Elements(tag_event).Elements().Where(x => (string)x.Attribute("key") == "concept:name");

                foreach (var el in elems_EventName)
                {
                    if (el.Attribute("value").Value.Equals("Appeal to Judge"))
                    {
                        iJudgeInvolved = 1;
                        break;
                    }
                }
                trace.SetAttributeValue("Label", iJudgeInvolved.ToString());
            }
        }

        public static void labelingRL(IEnumerable<XElement> traces)
        {
            //Labeling RL:
            //Last event of each process shows the result of the Review. 
            //It can take the values accept and reject. 
            foreach (var trace in traces)
            {
                int iAccept = 0;

                var elems_EventName = trace.Elements(tag_event).Elements().Where(x => (string)x.Attribute("key") == "concept:name");
                string lastEvent = elems_EventName.ElementAt(elems_EventName.Count() - 1).Attribute("value").Value;

                if (lastEvent.Equals("accept"))
                {
                    iAccept = 1;
                }
                else if(lastEvent.Equals("reject"))
                {
                    iAccept = 2;
                }

                trace.SetAttributeValue("Label", iAccept.ToString());
            }
        }
    }
}
