/*
 * 2012 Ted Spence, http://tedspence.com
 * License: http://www.apache.org/licenses/LICENSE-2.0 
 * Home page: https://code.google.com/p/csharp-command-line-wrapper
 */
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;

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
            if (a == null) a = Assembly.GetExecutingAssembly();

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
                    } else if (v.ParameterType == typeof(char)) {
                        thisparam = paramstr[0];
                    } else if (v.ParameterType == typeof(int)) {
                        thisparam = Int32.Parse(paramstr);
                    } else if (v.ParameterType == typeof(long)) {
                        thisparam = Int64.Parse(paramstr);
                    } else if (v.ParameterType == typeof(DateTime)) {
                        thisparam = DateTime.Parse(paramstr);
                    } else {
                        return ShowHelp(String.Format("Unsupported type {0}", v.ParameterType.FullName), m);
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
            // Show help
            if (!String.IsNullOrEmpty(syntax_error_message)) {
                Console.WriteLine("SYNTAX ERROR:");
                Console.WriteLine("    " + syntax_error_message);
                Console.WriteLine();
            }

            // Get all Copyright attributes on this assembly
            var ta = (from object a in Assembly.GetExecutingAssembly().GetCustomAttributes(typeof(AssemblyTitleAttribute), false) select a).First();
            string title = ta == null ? System.AppDomain.CurrentDomain.FriendlyName : ((AssemblyTitleAttribute)ta).Title;
            var ca = (from object a in Assembly.GetExecutingAssembly().GetCustomAttributes(typeof(AssemblyCopyrightAttribute), false) select a).First();
            string copyright = ca == null ? "" : ((AssemblyCopyrightAttribute)ca).Copyright + "\n";
            Console.WriteLine("{0}\n{1}", title, copyright);

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
            }

            // Return an appropriate error code for the application
            if (syntax_error_message != null) {
                return -1;
            } else {
                return 0;
            }
        }
        #endregion
    }
}
