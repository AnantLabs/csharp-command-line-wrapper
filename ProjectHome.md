This program provides a simplified command line wrapper for C# executables, providing a clean and consistent command-line interface plus a straightforward Windows GUI (WinForms) interface.

Simply add one file into your project, and your user interface work is done.  You can focus instead on just writing your code and logic, and keep the interface out of the way.

## Usage ##

To use CommandWrapLib, add the single file [CommandWrapLib.cs](http://csharp-command-line-wrapper.googlecode.com/svn/trunk/CommandWrapper/CommandWrapper/CommandWrapLib.cs) to your program.  The file contains a new `main()` function - this replaces the one contained in your application.  You can then designate any number of functions with the `[Wrap]` attribute; they will all be offered for execution via the command line with a clean, well-formatted interface.

```
[Wrap]
public static void Method1(DateTime start_period, int count, string recipient_email);

[Wrap(Name="MyFunction", Description="Does Foo and Bar work.")]
public static void Method2(string param1, int param2, byte param3);
```

When you run the program from the command line, you'll now see user friendly help text like the following (constructed from your assembly info!):

```
MyProgram 1.0.21.1
Copyright (C) 2012 MyCopyrightNotice

Your XML documentation text will be shown here, if it is available.

USAGE:
    Myprogram.exe [parameters]

METHODS:
    MyClass.MyMethod1
    MyClass.MyMethod2
    MyClass.MyMethod3
```

You can then choose to execute one of the functions.  If you execute the program and specify "MyClass.MyMethod1", it will then produce helpful parameter guidance (provided you've built XML documentation!):

```
PARAMETERS:
    --start_period=System.DateTime
        The beginning date for the report. 
    --count=System.Int32
        The end date for the report.
    --recipient_email=System.String
        The email address of the recipient.

```

You can now execute the function  from the command line specifying each parameter using normal parameter lists, like the following:

```
MyProgram.exe MyClass.Method1 --start_period=2012-01-01 --count=100 --recipient=test@test.com
```

If any parameter is mistyped, or invalid, the wrapper will detect this and display the appropriate error.  Optional parameters are allowed but not required.

Remember, if you want auto-complete documentation to appear in the console output, make sure to compile your program with XML documentation turned on!

## Windows Automation ##

CommandWrapLib now provides a windows GUI (WinForms) front-end for simplified execution.  You can still write your functions exactly as before.  When you execute the program with no options, it will automatically launch the Windows GUI mode and help you select your work.

## Notes ##

  * Complex parameter types are not yet supported.  This means you can't pass in an object, or a struct, or something else beyond a C# basic value type on the command line.  It's possible that we could support XML serialization to retrieve an object from XML - would that be worthwhile?
  * We haven't yet standardized any return values to display to the user the output of the function.
  * We could add notification support so that status, timings, etc, could be emailed to users or written to a database using Log4net or the equivalent.