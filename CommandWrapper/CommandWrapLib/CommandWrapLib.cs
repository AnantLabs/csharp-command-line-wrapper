/*
 * 2012 Ted Spence, http://tedspence.com
 * License: http://www.apache.org/licenses/LICENSE-2.0 
 * Home page: https://code.google.com/p/csharp-command-line-wrapper

 * Some portions retrieved from DocsByReflection by Jim Blackler: http://jimblackler.net/blog/?p=49
 * His copyright notice is:
//Except where stated all code and programs in this project are the copyright of Jim Blackler, 2008.
//jimblackler@gmail.com
//
//This is free software. Libraries and programs are distributed under the terms of the GNU Lesser
//General Public License. Please see the files COPYING and COPYING.LESSER.
 */
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using System.Xml;
using System.IO;

namespace CommandWrapLib
{
    public class CommandWrapLib
    {
        #region Public interface for a command line execution of an arbitrary function
        /// <summary>
        /// Wrap a specific class and function in a console interface library.
        /// </summary>
        /// <param name="classname">The class name within the executing assembly.</param>
        /// <param name="staticfunctionname">The static function name to execute on this class.</param>
        /// <param name="args">The list of arguments provided on the command line.</param>
        /// <returns>-1 if a failure occurred, 0 if the call succeeded.</returns>
        public static int ConsoleWrapper(Assembly a, string classname, string staticfunctionname, string[] args)
        {
            // Interpret "no assembly" as "currently executing assembly"
            if (a == null) a = Assembly.GetCallingAssembly();

            // Get the assembly and confirm we can do our work
            Type t = a.GetType(a.GetName().Name + "." + classname);
            if (t == null) {
                throw new Exception(String.Format("Class {0} does not exist within assembly {1}.", classname, a.FullName));
            }
            MethodInfo m = (from MethodInfo mi in t.GetMethods() where mi.Name == staticfunctionname && mi.IsStatic select mi).First();
            if (m == null) {
                throw new Exception(String.Format("No static method named {0} was found in class {1}.", staticfunctionname, classname));
            }
            ParameterInfo[] pilist = m.GetParameters();
            bool any_params_required = (from ParameterInfo pi in pilist where pi.IsOptional == false select pi).Any();

            // If no arguments, or if help is requested, show help
            if (args.Length == 1) {
                if (String.Equals(args[0], "-h", StringComparison.CurrentCultureIgnoreCase) ||
                    String.Equals(args[0], "--help", StringComparison.CurrentCultureIgnoreCase) ||
                    String.Equals(args[0], "/h", StringComparison.CurrentCultureIgnoreCase) ||
                    String.Equals(args[0], "/help", StringComparison.CurrentCultureIgnoreCase)) {
                    return ShowHelp(null, m);
                }
            }

            // If the user just executed the program without specifying any parameters
            if (args.Length == 0 && any_params_required) {
                return ShowHelp(null, m);
            }

            // Populate all the parameters from the arglist
            object[] callparams = new object[pilist.Length];
            for (int i = 0; i < args.Length; i++) {

                // If there's an equals, handle that
                string paramname, paramstr;
                int equalspos = args[i].IndexOf("=");
                if (equalspos > 0) {
                    paramname = args[i].Substring(0, equalspos);
                    paramstr = args[i].Substring(equalspos + 1);
                } else {
                    paramname = args[i];
                    if (i == args.Length - 1) {
                        return ShowHelp(String.Format("Missing value for {0}.", paramname), m);
                    }
                    i++;
                    paramstr = args[i];
                }

                // Clean up the argument
                if (paramname.StartsWith("--")) {
                    paramname = paramname.Substring(2);
                } else if (paramname.StartsWith("-")) {
                    paramname = paramname.Substring(1);
                }

                // Figure out what parameter this corresponds to
                var v = (from ParameterInfo pi in pilist where String.Equals(pi.Name, paramname, StringComparison.CurrentCultureIgnoreCase) select pi).FirstOrDefault();
                if (v == null) {
                    return ShowHelp(String.Format("Unrecognized option {0}", args[i]), m);
                }

                // Figure out its position in the call params
                int pos = Array.IndexOf(pilist, v);
                object thisparam = null;

                // Attempt to parse this parameter
                try {
                    if (v.ParameterType == typeof(string)) {
                        thisparam = paramstr;
                    } else if (v.ParameterType == typeof(bool)) {
                        thisparam = Boolean.Parse(paramstr);
                    } else if (v.ParameterType == typeof(byte)) {
                        thisparam = Byte.Parse(paramstr);
                    } else if (v.ParameterType == typeof(sbyte)) {
                        thisparam = SByte.Parse(paramstr);
                    } else if (v.ParameterType == typeof(short)) {
                        thisparam = Int16.Parse(paramstr);
                    } else if (v.ParameterType == typeof(ushort)) {
                        thisparam = UInt16.Parse(paramstr);
                    } else if (v.ParameterType == typeof(int)) {
                        thisparam = Int32.Parse(paramstr);
                    } else if (v.ParameterType == typeof(uint)) {
                        thisparam = UInt32.Parse(paramstr);
                    } else if (v.ParameterType == typeof(long)) {
                        thisparam = Int64.Parse(paramstr);
                    } else if (v.ParameterType == typeof(ulong)) {
                        thisparam = UInt64.Parse(paramstr);
                    } else if (v.ParameterType == typeof(float)) {
                        thisparam = Single.Parse(paramstr);
                    } else if (v.ParameterType == typeof(double)) {
                        thisparam = Double.Parse(paramstr);
                    } else if (v.ParameterType == typeof(decimal)) {
                        thisparam = Decimal.Parse(paramstr);
                    } else if (v.ParameterType == typeof(char)) {
                        thisparam = paramstr[0];
                    } else if (v.ParameterType == typeof(DateTime)) {
                        thisparam = DateTime.Parse(paramstr);
                    } else {
                        return ShowHelp(String.Format("Unsupported type {0} - only basic value types can be parsed from the command line.", v.ParameterType.FullName), m);
                    }
                } catch {
                    return ShowHelp(String.Format("The value {0} is not valid for {1} - required '{2}'", paramstr, args[i], v.ParameterType.FullName), m);
                }

                // Did we fail to get a parameter?
                if (thisparam == null) {
                    throw new Exception(String.Format("Parameter {0} requires the complex type {1}, and cannot be passed on the command line.", v.Name, v.ParameterType.FullName));
                }
                callparams[pos] = thisparam;
            }

            // Ensure all mandatory parameters are filled in
            for (int i = 0; i < pilist.Length; i++) {
                if (!pilist[i].IsOptional && (callparams[i] == null)) {
                    return ShowHelp(String.Format("Missing required parameter {0}", pilist[i].Name), m);
                }
            }

            // Execute this call and display the result (if any)
            object result = m.Invoke(null, callparams);
            if (result != null) {
                Console.WriteLine("RESULT: " + result.ToString());
            }
            return 0;
        }
        #endregion

        #region Helper Functions
        /// <summary>
        /// Show the most useful possible command line help
        /// </summary>
        /// <param name="syntax_error_message">Provide feedback on a user error</param>
        /// <param name="m">The method we should provide usage information for.</param>
        /// <returns>0 if successful, -1 if a syntax error was shown.</returns>
        private static int ShowHelp(string syntax_error_message, MethodInfo m)
        {
            // Is it possible to get some documentation?
            XmlElement documentation = null;
            try {
                documentation = XMLFromMember(m);
            } catch {
                System.Diagnostics.Debug.WriteLine("XML Help is not available.  Please compile your program with XML documentation turned on if you wish to use XML documentation.");
            }
 
            // Show help
            if (!String.IsNullOrEmpty(syntax_error_message)) {
                Console.WriteLine("SYNTAX ERROR:");
                Console.WriteLine("    " + syntax_error_message);
                Console.WriteLine();
            }

            // Gather copyright and various details
            var ta = (from object a in Assembly.GetExecutingAssembly().GetCustomAttributes(typeof(AssemblyTitleAttribute), false) select a).First();
            string title = ta == null ? System.AppDomain.CurrentDomain.FriendlyName : ((AssemblyTitleAttribute)ta).Title;
            var ca = (from object a in Assembly.GetExecutingAssembly().GetCustomAttributes(typeof(AssemblyCopyrightAttribute), false) select a).First();
            string copyright = ca == null ? "" : ((AssemblyCopyrightAttribute)ca).Copyright.Replace("©", "(C)") + "\n";

            // Show copyright
            Console.WriteLine("{0}\n{1}", title, copyright);
            if (documentation != null) {
                Console.WriteLine(documentation["summary"].InnerText.Trim());
                Console.WriteLine();
            }

            // Show the definition of the function
            Console.WriteLine("USAGE:");
            Console.Write("    ");
            Console.Write(System.AppDomain.CurrentDomain.FriendlyName);
            Console.WriteLine(" [parameters]");
            Console.WriteLine();

            // Show full definition of parameters
            Console.WriteLine("PARAMETERS:");
            foreach (ParameterInfo pi in m.GetParameters()) {
                if (pi.IsOptional) {
                    Console.WriteLine("    [--{0}={1}] (optional)", pi.Name, pi.ParameterType);
                } else {
                    Console.WriteLine("    --{0}={1}", pi.Name, pi.ParameterType);
                }

                // Show help for the parameters, if they are available
                if (documentation != null) {
                    XmlNode el = documentation.SelectSingleNode("//param[@name=\"" + pi.Name + "\"]");
                    if (el != null) {
                        Console.WriteLine("        " + el.InnerText);
                    }
                }
            }

            // Return an appropriate error code for the application
            if (syntax_error_message != null) {
                return -1;
            } else {
                return 0;
            }
        }
        #endregion

        #region Jim Blackler's Docs By Reflection code, added here to make copying and pasting this code easier
        /// <summary>
        /// Provides the documentation comments for a specific method
        /// </summary>
        /// <param name="methodInfo">The MethodInfo (reflection data ) of the member to find documentation for</param>
        /// <returns>The XML fragment describing the method</returns>
        public static XmlElement XMLFromMember(MethodInfo methodInfo)
        {
            // Calculate the parameter string as this is in the member name in the XML
            string parametersString = "";
            foreach (ParameterInfo parameterInfo in methodInfo.GetParameters()) {
                if (parametersString.Length > 0) {
                    parametersString += ",";
                }

                parametersString += parameterInfo.ParameterType.FullName;
            }

            //AL: 15.04.2008 ==> BUG-FIX remove “()” if parametersString is empty
            if (parametersString.Length > 0) {
                return XMLFromName(methodInfo.DeclaringType, 'M', methodInfo.Name + "(" + parametersString + ")");
            } else {
                return XMLFromName(methodInfo.DeclaringType, 'M', methodInfo.Name);
            }
        }

        /// <summary>
        /// Provides the documentation comments for a specific member
        /// </summary>
        /// <param name="memberInfo">The MemberInfo (reflection data) or the member to find documentation for</param>
        /// <returns>The XML fragment describing the member</returns>
        public static XmlElement XMLFromMember(MemberInfo memberInfo)
        {
            // First character [0] of member type is prefix character in the name in the XML
            return XMLFromName(memberInfo.DeclaringType, memberInfo.MemberType.ToString()[0], memberInfo.Name);
        }

        /// <summary>
        /// Provides the documentation comments for a specific type
        /// </summary>
        /// <param name="type">Type to find the documentation for</param>
        /// <returns>The XML fragment that describes the type</returns>
        public static XmlElement XMLFromType(Type type)
        {
            // Prefix in type names is T
            return XMLFromName(type, 'T', "");
        }

        /// <summary>
        /// Obtains the XML Element that describes a reflection element by searching the 
        /// members for a member that has a name that describes the element.
        /// </summary>
        /// <param name="type">The type or parent type, used to fetch the assembly</param>
        /// <param name="prefix">The prefix as seen in the name attribute in the documentation XML</param>
        /// <param name="name">Where relevant, the full name qualifier for the element</param>
        /// <returns>The member that has a name that describes the specified reflection element</returns>
        private static XmlElement XMLFromName(Type type, char prefix, string name)
        {
            string fullName;

            if (String.IsNullOrEmpty(name)) {
                fullName = prefix + ":" + type.FullName;
            } else {
                fullName = prefix + ":" + type.FullName + "." + name;
            }

            XmlDocument xmlDocument = XMLFromAssembly(type.Assembly);

            XmlElement matchedElement = null;

            foreach (XmlElement xmlElement in xmlDocument["doc"]["members"]) {
                if (xmlElement.Attributes["name"].Value.Equals(fullName)) {
                    if (matchedElement != null) {
                        throw new Exception("Multiple matches to query");
                    }

                    matchedElement = xmlElement;
                }
            }

            if (matchedElement == null) {
                throw new Exception("Could not find documentation for specified element");
            }

            return matchedElement;
        }

        /// <summary>
        /// A cache used to remember Xml documentation for assemblies
        /// </summary>
        static Dictionary<Assembly, XmlDocument> cache = new Dictionary<Assembly, XmlDocument>();

        /// <summary>
        /// A cache used to store failure exceptions for assembly lookups
        /// </summary>
        static Dictionary<Assembly, Exception> failCache = new Dictionary<Assembly, Exception>();

        /// <summary>
        /// Obtains the documentation file for the specified assembly
        /// </summary>
        /// <param name="assembly">The assembly to find the XML document for</param>
        /// <returns>The XML document</returns>
        /// <remarks>This version uses a cache to preserve the assemblies, so that 
        /// the XML file is not loaded and parsed on every single lookup</remarks>
        public static XmlDocument XMLFromAssembly(Assembly assembly)
        {
            if (failCache.ContainsKey(assembly)) {
                throw failCache[assembly];
            }

            try {

                if (!cache.ContainsKey(assembly)) {
                    // load the docuemnt into the cache
                    cache[assembly] = XMLFromAssemblyNonCached(assembly);
                }

                return cache[assembly];
            } catch (Exception exception) {
                failCache[assembly] = exception;
                throw exception;
            }
        }

        /// <summary>
        /// Loads and parses the documentation file for the specified assembly
        /// </summary>
        /// <param name="assembly">The assembly to find the XML document for</param>
        /// <returns>The XML document</returns>
        private static XmlDocument XMLFromAssemblyNonCached(Assembly assembly)
        {
            string assemblyFilename = assembly.CodeBase;

            const string prefix = "file:///";

            if (assemblyFilename.StartsWith(prefix)) {
                StreamReader streamReader;

                try {
                    streamReader = new StreamReader(Path.ChangeExtension(assemblyFilename.Substring(prefix.Length), ".xml"));
                } catch (FileNotFoundException exception) {
                    throw new Exception("XML documentation not present (make sure it is turned on in project properties when building)", exception);
                }

                XmlDocument xmlDocument = new XmlDocument();
                xmlDocument.Load(streamReader);
                return xmlDocument;
            } else {
                throw new Exception("Could not ascertain assembly filename");
            }
        }
        #endregion
    }
}
