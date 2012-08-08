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
        static void Main(string[] args)
        {
            // Show help if no parameters specified
            if (args.Length == 0) {
                ShowHelp();
                return;
            }

            // Interpret parameter 0 as the {assembly}.{class}.{method}
            string dll = args[0];
            if (!File.Exists(dll)) {
                Console.WriteLine("Unable to find file: " + dll);
                ShowHelp();
                return;
            }
            Assembly a = Assembly.LoadFrom(dll);
            if (a == null) {
                Console.WriteLine("Unable to load assembly from file: " + dll);
                ShowHelp();
                return;
            }

            // Okay, let's attempt to parse this
            string[] breakdown = args[1].Split('.');
            if (breakdown.Length == 2) {
                CommandWrapLib.ConsoleWrapper(a, breakdown[0], breakdown[1], args.Skip(2).ToArray());

            // Find the assembly specified by the caller
            } else if (breakdown.Length == 3) {
                string classid = breakdown[0] + "." + breakdown[1];
                Assembly found = Assembly.GetAssembly(Type.GetType(classid));
                if (found == null) {
                    Console.WriteLine("Class {0} wasn't found in the loaded assemblies.", classid);
                    ShowHelp();
                    return;
                }
                CommandWrapLib.ConsoleWrapper(a, breakdown[0], breakdown[1], args.Skip(3).ToArray());
            } else {
                Console.WriteLine("Please specify the function to execute as either 'assembly.class.func' or 'class.func'.");
                ShowHelp();
                return;
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
