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
using System.IO;

namespace CommandWrapper
{
    class Program
    {
        /// <summary>
        /// Launch an external DLL file and call a specific static method on a class within that assembly.
        /// </summary>
        /// <param name="DllName">The file & path to the DLL.</param>
        /// <param name="Class">The class name to execute.</param>
        /// <param name="Method">The method name to execute.  Must be static.</param>
        /// <param name="args">Comma-separated list of arguments.</param>
        [Wrap]
        public static void WrapOutsideAssembly(string DllName, string Class = null, string Method = null, string args = null)
        {
            // Interpret parameter 0 as the {assembly}.{class}.{method}
            if (!File.Exists(DllName)) {
                Console.WriteLine("Unable to find file: " + DllName);
                ShowHelp();
                return;
            }
            Assembly a = Assembly.LoadFrom(DllName);
            if (a == null) {
                Console.WriteLine("Unable to load assembly from file: " + DllName);
                ShowHelp();
                return;
            }

            // Okay, let's attempt to parse this
            if (Class == null || Method == null) {
            } else {
                CommandWrapLib.ConsoleWrapper(a, Class, Method, args.Split(','));
            }
        }

        private static void ShowHelp()
        {
            Console.WriteLine("CommandWrapper");
            Console.WriteLine("(C) 2012 Ted Spence, http://tedspence.com");
            Console.WriteLine();
            Console.WriteLine("USAGE:");
            Console.WriteLine("    CommandWrapper.exe [dll] [assembly.class.func OR class.func] [arguments]");
            Console.WriteLine();
            Console.WriteLine("PARAMETERS:");
            Console.WriteLine("    [dll]: The filename of a DLL to open, which contains the class you wish");
            Console.WriteLine("         to call.  Must be a C# DLL.");
            Console.WriteLine("    [assembly.class.function]: The fully qualified assembly name, class, ");
            Console.WriteLine("        and function you wish to execute.");
            Console.WriteLine("    [class.function]: The class and function you wish to execute; assuming");
            Console.WriteLine("        that the class exists within DLL name you specified.");
        }
    }
}
