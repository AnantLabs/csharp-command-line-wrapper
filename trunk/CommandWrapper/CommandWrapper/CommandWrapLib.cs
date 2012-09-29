/*
 * 2012 Ted Spence, http://tedspence.com
 * License: http://www.apache.org/licenses/LICENSE-2.0 
 * Home page: https://code.google.com/p/csharp-command-line-wrapper

 * Some portions retrieved from DocsByReflection by Jim Blackler: http://jimblackler.net/blog/?p=49
 * His copyright notice is:
 * 
//Except where stated all code and programs in this project are the copyright of Jim Blackler, 2008.
//jimblackler@gmail.com
//
//This is free software. Libraries and programs are distributed under the terms of the GNU Lesser
//General Public License. Please see the files COPYING and COPYING.LESSER.
 * 
 */
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using System.Xml;
using System.IO;
using System.ComponentModel;

/// <summary>
/// This is the command wrap class - it does not have a namespace definition to prevent complications if you "drop it" directly into an existing project.
/// </summary>
public static class CommandWrapLib
{
    #region Wrapper Library Variables

    /// <summary>
    /// If the user requests that we log the output of the task (using "-L folder"), here's where we go
    /// </summary>
    private static string _log_folder;
    private static StreamWriter _log_writer;

    /// <summary>
    /// Wrapper class for our output redirect
    /// </summary>
    private class OutputRedirect : TextWriter
    {
        public string Name;
        public TextWriter OldWriter;

        public override Encoding Encoding
        {
            get { return OldWriter.Encoding; }
        }

        public override void Write(char[] buffer, int index, int count)
        {
            // Did the user redirect our output to a log file?  If so, do it!
            if (_log_writer != null) {
                string s = new string(buffer, index, count);
                _log_writer.Write(String.Format("{0} {1} {2}", DateTime.Now, Name, s));
                _log_writer.Flush();
            }

            // Then write our text
            OldWriter.Write(buffer, index, count);
        }
    }
    #endregion

    #region Generic public interface for wrapping an arbitrary number of functions
    /// <summary>
    /// Looks through the list of public static interfaces and offers a choice of functions to call
    /// </summary>
    /// <param name="args"></param>
    public static void Main(string[] args)
    {
        // Use the main assembly
        Assembly a = Assembly.GetEntryAssembly();

        // Can we find the type and method?
        Dictionary<string, MatchingMethods> wrapped_calls = new Dictionary<string, MatchingMethods>();
        Dictionary<string, MatchingMethods> all_calls = new Dictionary<string, MatchingMethods>();
        foreach (Type atype in a.GetTypes()) {
            if (atype == typeof(CommandWrapLib)) continue;

            // Iterate through all static methods and try them
            var methods = (from MethodInfo mi in atype.GetMethods() where mi.IsStatic orderby mi.GetParameters().Count() descending select mi);
            if (methods != null && methods.Count() > 0) {
                foreach (MethodInfo mi in methods) {

                    // Retrieve the call and wrap names
                    string call = atype.Name + "." + mi.Name;
                    bool is_wrapped = false;
                    string wrap = null;
                    foreach (Attribute attr in mi.GetCustomAttributes(true)) {
                        if (attr is Wrap) {
                            is_wrapped = true;
                            wrap = ((Wrap)attr).Name;
                            if (String.IsNullOrEmpty(wrap)) wrap = call;
                        }
                    }

                    // Record this function in the "all static calls" list
                    MatchingMethods mm = null;
                    all_calls.TryGetValue(call, out mm);
                    if (mm == null) mm = new MatchingMethods();
                    mm.Methods.Add(mi);
                    all_calls[call] = mm;

                    // Record this function in the "wrapped calls" list if appropriate
                    if (is_wrapped) {
                        wrapped_calls.TryGetValue(wrap, out mm);
                        if (mm == null) mm = new MatchingMethods();
                        mm.Methods.Add(mi);
                        wrapped_calls[wrap] = mm;
                    }
                }
            }
        }

        // We didn't find a valid call - notify the user of all the possibilities.
        if (wrapped_calls.Count == 0) {
            System.Diagnostics.Debug.WriteLine("You did not apply the [Wrap] attribute to any functions.  I will show all possible static functions within this assembly.  To filter the list of options down, please apply the [Wrap] attribute to the functions you wish to be callable from the command line.");
            wrapped_calls = all_calls;
        } 
        
        // There was only one wrapped call - assume we're calling that!
        if (wrapped_calls.Count == 1) {
            TryAllMethods(a, wrapped_calls.Values.ToArray()[0], args);
            return;
        }

        // Did the user provide any arguments?  If so, try to interpret in a way that results in a function call
        if (args.Length > 0) {

            // If we have arguments, let's attempt to call the matching one of them
            MatchingMethods mm = null;
            if (wrapped_calls.TryGetValue(args[0], out mm)) {
                TryAllMethods(a, mm, args.Skip(1).ToArray());
                return;
            }

            // We didn't find a match; show general help
            ShowHelp(String.Format("Method '{0}' is not recognized.", args[0]), a, wrapped_calls.Keys.ToList());

        // User didn't specify anything, just show general help
        } else {
            ShowHelp(null, a, wrapped_calls.Keys.ToList());
        }
    }
    #endregion

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

        // Get the assembly and search through types - note that we're using a search through the array rather than "GetType" since in some cases the assembly
        // name can be munged in ways that are unpredictable
        Type t = null;
        foreach (Type atype in a.GetTypes()) {
            if (String.Equals(atype.Name, classname)) {
                t = atype;
                break;
            }
        }
        if (t == null) {
            throw new Exception(String.Format("Class {0} does not exist within assembly {1}.", classname, a.FullName));
        }

        // Okay, let's find a potential list of methods that could fit our needs and see if any of them work
        var methods = (from MethodInfo mi in t.GetMethods() where mi.Name == staticfunctionname && mi.IsStatic orderby mi.GetParameters().Count() descending select mi);
        if (methods == null || methods.Count() == 0) {
            throw new Exception(String.Format("No static method named {0} was found in class {1}.", staticfunctionname, classname));
        }

        // For thoroughness, let's pick which method is the biggest one
        var biggest_method = (from MethodInfo mi in methods select mi).First();

        // If no arguments, or if help is requested, show help
        if (args.Length == 1) {
            if (String.Equals(args[0], "-h", StringComparison.CurrentCultureIgnoreCase) ||
                String.Equals(args[0], "--help", StringComparison.CurrentCultureIgnoreCase) ||
                String.Equals(args[0], "/h", StringComparison.CurrentCultureIgnoreCase) ||
                String.Equals(args[0], "/help", StringComparison.CurrentCultureIgnoreCase)) {
                return ShowHelp(null, a, biggest_method);
            }
        }

        // Attempt each potential method that matches the signature; if any one succeeds, done!
        foreach (var method in methods) {
            if (TryMethod(a, method, args, false)) {
                return 0;
            }
        }

        // Okay, we couldn't succeed according to any of the methods.  Let's pick the one with the most parameters and show help for it
        TryMethod(a, biggest_method, args, true);
        return -1;
    }

    /// <summary>
    /// Try all methods from a matching list
    /// </summary>
    /// <param name="a"></param>
    /// <param name="matchingMethods"></param>
    /// <param name="args"></param>
    /// <param name="p"></param>
    private static void TryAllMethods(Assembly a, MatchingMethods methods_to_try, string[] args)
    {
        // Try each method once
        foreach (MethodInfo mi in methods_to_try.Methods) {
            if (TryMethod(a, mi, args, false)) {
                return;
            }
        }

        // No calls succeeded; show help for the biggest method
        MethodInfo big = methods_to_try.GetBiggestMethod();
        TryMethod(a, big, args, true);
    }

    /// <summary>
    /// Inner helper function that attempts to make our parameters match a specific method
    /// </summary>
    /// <param name="a"></param>
    /// <param name="m"></param>
    /// <param name="args"></param>
    /// <param name="show_help_on_failure"></param>
    /// <returns></returns>
    private static bool TryMethod(Assembly a, MethodInfo m, string[] args, bool show_help_on_failure)
    {
        ParameterInfo[] pilist = m.GetParameters();
        bool any_params_required = (from ParameterInfo pi in pilist where pi.IsOptional == false select pi).Any();

        // If the user just executed the program without specifying any parameters
        if (args.Length == 0 && any_params_required) {
            if (show_help_on_failure) ShowHelp(null, a, m);
            return false;
        }

        // Populate all the parameters from the arglist
        object[] callparams = new object[pilist.Length];
        for (int i = 0; i < args.Length; i++) {
            string thisarg = args[i];

            // Parameters with a double-hyphen are function parameters
            if (thisarg.StartsWith("--")) {
                thisarg = thisarg.Substring(2);

                // If there's an equals, handle that
                string paramname, paramstr;
                int equalspos = thisarg.IndexOf("=");
                if (equalspos > 0) {
                    paramname = thisarg.Substring(0, equalspos);
                    paramstr = thisarg.Substring(equalspos + 1);
                } else {
                    paramname = thisarg;
                    if (i == args.Length - 1) {
                        if (show_help_on_failure) ShowHelp(String.Format("Missing value for {0}.", paramname), a, m);
                        return false;
                    }
                    i++;
                    paramstr = thisarg;
                }

                // Figure out what parameter this corresponds to
                var v = (from ParameterInfo pi in pilist where String.Equals(pi.Name, paramname, StringComparison.CurrentCultureIgnoreCase) select pi).FirstOrDefault();
                if (v == null) {
                    if (show_help_on_failure) ShowHelp(String.Format("Unrecognized option {0}", args[i]), a, m);
                    return false;
                }

                // Figure out its position in the call params
                int pos = Array.IndexOf(pilist, v);
                object thisparam = null;

                // Attempt to parse this parameter
                try {
                    try {
                        if (v.ParameterType == typeof(Guid)) {
                            thisparam = Guid.Parse(paramstr);
                        } else {
                            thisparam = Convert.ChangeType(paramstr, v.ParameterType);
                        }
                    } catch (Exception ex) {
                        if (show_help_on_failure) ShowHelp(String.Format("Unable to convert '{0}' into type {1}.\n\n{2}\n\n", paramstr, v.ParameterType.FullName, ex.ToString()), a, m);
                        return false;
                    }
                } catch {
                    if (show_help_on_failure) ShowHelp(String.Format("The value {0} is not valid for {1} - required '{2}'", paramstr, args[i], v.ParameterType.FullName), a, m);
                    return false;
                }

                // Did we fail to get a parameter?
                if (thisparam == null) {
                    throw new Exception(String.Format("Parameter {0} requires the complex type {1}, and cannot be passed on the command line.", v.Name, v.ParameterType.FullName));
                }
                callparams[pos] = thisparam;

            // Any parameter with a single hyphen is a "WrapLib" parameter
            } else if (thisarg.StartsWith("-")) {
                char wrap_param = thisarg[1];

                // Which parameter did the user pass?
                switch (wrap_param) {

                    // Log to a folder
                    case 'L':
                        if (i == args.Length - 1) {
                            ShowHelp("Missing log folder name for '-L' option.  Please specify '-L <folder>'.", a, m);
                            return false;
                        }

                        // Consume the next parameter
                        i++;
                        _log_folder = args[i];
                        if (!Directory.Exists(_log_folder)) {
                            Console.WriteLine("Creating log folder {0}", _log_folder);
                            Directory.CreateDirectory(_log_folder);
                        }

                        // The task will begin logging when the call succeeds
                        break;

                    // Unrecognized option
                    default:
                        ShowHelp(String.Format("Unrecognized option '-{0}'.", wrap_param), a, m);
                        return false;
                }
            }
        }

        // Ensure all mandatory parameters are filled in
        for (int i = 0; i < pilist.Length; i++) {
            if (!pilist[i].IsOptional && (callparams[i] == null)) {
                if (show_help_on_failure) ShowHelp(String.Format("Missing required parameter {0}", pilist[i].Name), a, m);
                return false;
            }
        }

        // Execute this call and display the result (if any), plus its type
        DateTime start_time = DateTime.Now;
        object result = null;
        try {

            // Okay, we're about to invoke!  Did the user want us to log the output?
            try {
                if (!String.IsNullOrEmpty(_log_folder)) {
                    string logfilename = null;
                    while (true) {
                        logfilename = Path.Combine(_log_folder, DateTime.Now.ToString("o").Replace(':', '_') + ".log");
                        if (!File.Exists(logfilename)) break;
                        System.Threading.Thread.Sleep(10);
                    }
                    _log_writer = new StreamWriter(logfilename);
                }

                // Create a redirect for STDOUT & STDERR
                OutputRedirect StdOutRedir = new OutputRedirect() { Name = "STDOUT", OldWriter = Console.Out };
                OutputRedirect StdErrRedir = new OutputRedirect() { Name = "STDERR", OldWriter = Console.Error };
                try {
                    Console.SetOut(StdOutRedir);
                    Console.SetError(StdErrRedir);

                    // Execute our class
                    m.Invoke(null, callparams);
                    if (result != null) {
                        Console.WriteLine("RESULT: {0} ({1})", result, result.GetType());
                    }

                // Reset the standard out and standard error - this ensures no future errors after execution
                } finally {
                    Console.SetOut(StdOutRedir.OldWriter);
                    Console.SetError(StdErrRedir.OldWriter);
                }

            // Close gracefully
            } finally {
                if (_log_writer != null) _log_writer.Close();
            }

        // Show some useful diagnostics
        } catch (Exception ex) {
            Console.WriteLine("EXCEPTION: " + ex.ToString());
        }
        Console.WriteLine("DURATION: {0}", DateTime.Now - start_time);
        return true;
    }
    #endregion

    #region Helper Functions
    /// <summary>
    /// Show help when there are a variety of possible calls you could make
    /// </summary>
    /// <param name="syntax_error_message"></param>
    /// <param name="a"></param>
    /// <param name="possible_calls"></param>
    private static int ShowHelp(string syntax_error_message, Assembly a, List<string> possible_calls)
    {
        // Build the "advice" section
        StringBuilder advice = new StringBuilder();

        // Show all possible methods
        advice.AppendLine("USAGE:");
        advice.AppendFormat("    {0} [method] [parameters]\n", System.AppDomain.CurrentDomain.FriendlyName);
        advice.AppendLine();

        // Show all possible methods
        advice.AppendLine("METHODS:");
        foreach (string call in possible_calls) {
            advice.AppendLine("    " + call);
        }

        // Shell to the root function
        return ShowHelp(syntax_error_message, a, null, advice.ToString());
    }

    /// <summary>
    /// Internal help function - presumes "advice" is already known
    /// </summary>
    /// <param name="syntax_error_message"></param>
    /// <param name="a"></param>
    /// <param name="advice"></param>
    /// <returns></returns>
    private static int ShowHelp(string syntax_error_message, Assembly a, string application_summary, string advice)
    {
        // Get the application's title (or executable name)
        var v = a.GetMetadata<AssemblyTitleAttribute>();
        string title = v == null ? System.AppDomain.CurrentDomain.FriendlyName : v.Title;

        // Get the application's copyright (or blank)
        var ca = a.GetMetadata<AssemblyCopyrightAttribute>();
        string copyright = ca == null ? "" : ca.Copyright.Replace("©", "(C)");

        // Get the application's version
        var ver = a.GetMetadata<AssemblyFileVersionAttribute>();
        string version = ver == null ? "" : ver.Version;

        // Show copyright
        Console.WriteLine("{0} {1}\n{2}", title, version, copyright);
        Console.WriteLine();
        if (application_summary != null) {
            Console.WriteLine(application_summary.Trim());
            Console.WriteLine();
        }

        // Show advice
        Console.Write(advice);

        // Show help
        if (!String.IsNullOrEmpty(syntax_error_message)) {
            Console.WriteLine();
            Console.WriteLine("SYNTAX ERROR:");
            Console.WriteLine("    " + syntax_error_message);
        }

        // Return a failure code (-1) if there was a syntax issue
        return String.IsNullOrEmpty(syntax_error_message) ? 0 : -1;
    }

    /// <summary>
    /// Show the most useful possible command line help
    /// </summary>
    /// <param name="syntax_error_message">Provide feedback on a user error</param>
    /// <param name="m">The method we should provide usage information for.</param>
    /// <returns>0 if successful, -1 if a syntax error was shown.</returns>
    private static int ShowHelp(string syntax_error_message, Assembly a, MethodInfo m)
    {
        ParameterInfo[] plist = m.GetParameters();
        StringBuilder advice = new StringBuilder();

        // Is it possible to get some documentation?
        XmlElement documentation = null;
        try {
            documentation = XMLFromMember(m);
        } catch {
            System.Diagnostics.Debug.WriteLine("XML Help is not available.  Please compile your program with XML documentation turned on if you wish to use XML documentation.");
        }

        // Show the definition of the function
        advice.AppendLine("USAGE:");
        advice.AppendFormat("    {0} {1}\n", System.AppDomain.CurrentDomain.FriendlyName, plist.Length > 0 ? "[parameters]" : "");
        advice.AppendLine();

        // Show full definition of parameters
        if (plist.Length > 0) {
            advice.AppendLine("PARAMETERS:");
            foreach (ParameterInfo pi in m.GetParameters()) {
                if (pi.IsOptional) {
                    advice.AppendFormat("    [--{0}={1}] (optional)\n", pi.Name, pi.ParameterType);
                } else {
                    advice.AppendFormat("    --{0}={1}\n", pi.Name, pi.ParameterType);
                }

                // Show help for the parameters, if they are available
                if (documentation != null) {
                    XmlNode el = documentation.SelectSingleNode("//param[@name=\"" + pi.Name + "\"]");
                    if (el != null) {
                        advice.AppendFormat("        {0}\n", el.InnerText);
                    }
                }
            }
        }

        // Return an appropriate error code for the application
        string summary = null;
        if (documentation != null) {
            summary = documentation["summary"].InnerText;
        }
        return ShowHelp(syntax_error_message, a, summary, advice.ToString());
    }

    /// <summary>
    /// Ability to return assembly information as simply as possible
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="a"></param>
    /// <returns></returns>
    public static T GetMetadata<T>(this Assembly a)
    {
        return (T)(from object attr in a.GetCustomAttributes(typeof(T), false) select attr).FirstOrDefault();
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

        foreach (XmlNode xmlNode in xmlDocument["doc"]["members"]) {
            if (xmlNode is XmlElement) {
                XmlElement el = xmlNode as XmlElement;
                if (el.Attributes["name"].Value.Equals(fullName)) {
                    if (matchedElement != null) {
                        throw new Exception("Multiple matches to query");
                    }

                    matchedElement = el as XmlElement;
                }
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

/// <summary>
/// This is a tag you can use to specify which functions should be wrapped
/// </summary>
public class Wrap : Attribute
{
    /// <summary>
    /// The displayed name of this wrapped function
    /// </summary>
    public string Name = null;

    /// <summary>
    /// The rich description of the wrapped function (overrides the XML help text if defined).
    /// </summary>
    public string Description = null;
}

public class MatchingMethods
{
    public List<MethodInfo> Methods;

    public MatchingMethods()
    {
        Methods = new List<MethodInfo>();
    }

    public MethodInfo GetBiggestMethod()
    {
        return (from MethodInfo mi in Methods select mi).First();
    }
}

