/*
* Arguments class: application arguments interpreter
*
* Authors:      R. LOPES
* Created:      25 October 2002
*
* http://www.codeproject.com/Articles/3111/C-NET-Command-Line-Arguments-Parser
* Based on code above, but modernized and updated - modified by Hakan
*/

using System.Text.RegularExpressions;

namespace CommandLine.Utility
{
    /// <summary>
    /// Arguments class
    /// </summary>
    public class Arguments : System.Collections.Generic.Dictionary<string, string>
    {
        private const string DefaultValue = "true";

        public Arguments(string[] args, bool appendDuplicates = false)
        {
            var splitter = new Regex(@"^-{1,2}|^/|=", RegexOptions.IgnoreCase | RegexOptions.Compiled);
            var remover = new Regex(@"^['""]?(.*?)['""]?$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
            string parameter = null;

            // Valid parameters forms:
            // {-,/,--}param{ ,=,:}((",')value(",'))
            // Examples: -param1 value1 --param2 /param3:"Test-:-work" /param4=happy -param5 '--=nice=--'
            foreach (string txt in args)
            {
                // Look for new parameters (-,/ or --) and a possible enclosed value (=,:)
                var parts = splitter.Split(txt, 3);
                switch (parts.Length)
                {
                    // Found a value (for the last parameter found (space separator))
                    case 1:
                        if (parameter != null)
                        {
                            parts[0] = remover.Replace(parts[0], "$1");
                            this.AddOrAppendParameter(parameter, parts[0], appendDuplicates);

                            parameter = null;
                        }

                        // else Error: no parameter waiting for a value (skipped)
                        break;

                    // Found just a parameter
                    case 2:
                        // The last parameter is still waiting. With no value, set it to true.
                        if (parameter != null)
                        {
                            this.AddOrAppendParameter(parameter, DefaultValue, appendDuplicates);
                        }
                        else
                        {
                            if (!string.IsNullOrEmpty(parts[0]) && !string.IsNullOrEmpty(parts[1]))
                            {
                                // Parameter without prefix
                                this.AddOrAppendParameter(parts[0], parts[1], appendDuplicates);
                            }
                            else
                                parameter = parts[1];
                        }

                        break;

                    // Parameter with enclosed value
                    case 3:
                        // The last parameter is still waiting. With no value, set it to true.
                        if (parameter != null)
                        {
                            this.AddOrAppendParameter(parameter, DefaultValue, appendDuplicates);
                        }

                        parameter = parts[1];

                        // Remove possible enclosing characters (",')
                        if (!this.ContainsKey(parameter))
                        {
                            parts[2] = remover.Replace(parts[2], "$1");
                            this.AddOrAppendParameter(parameter, parts[2], appendDuplicates);
                        }

                        parameter = null;
                        break;
                }
            }

            // In case a parameter is still waiting
            if (parameter != null)
            {
                this.AddOrAppendParameter(parameter, DefaultValue, appendDuplicates);
            }
        }

        public bool IsBool(string parameter)
        {
            string value;
            if (!this.TryGetValue(parameter, out value))
                return false;

            bool result;
            if (!bool.TryParse(value, out result))
                return false;

            return result;
        }

        public string GetValueOrDefault(string parameter, string defaultValue = "")
        {
            string value;
            if (!this.TryGetValue(parameter, out value))
                return defaultValue;
            return value ?? string.Empty;
        }

        private void AddOrAppendParameter(string name, string value, bool appendIfExists)
        {
            string currentValue;
            if (!this.TryGetValue(name, out currentValue))
                this.Add(name, value);
            else
            {
                if (appendIfExists)
                    this[name] = currentValue + "," + value;
            }
        }
    }
}