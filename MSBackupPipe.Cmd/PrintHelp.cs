using System;
using System.Collections.Generic;
using System.Linq;
using MSBackupPipe.StdPlugins;

namespace MSBackupPipe.Cmd
{
    public static class PrintHelp
    {
        public static void PrintUsage()
        {
            Console.WriteLine("Below are the commands for msbp.exe:");
            Console.WriteLine("\tmsbp.exe help");
            Console.WriteLine("\tmsbp.exe backup");
            Console.WriteLine("\tmsbp.exe restore");
            Console.WriteLine("\tmsbp.exe restoreverifyonly");
            Console.WriteLine("\tmsbp.exe restoreheaderonly");
            Console.WriteLine("\tmsbp.exe restorefilelistonly");
            Console.WriteLine("\tmsbp.exe listplugins");
            Console.WriteLine("\tmsbp.exe helpplugin");
            Console.WriteLine("\tmsbp.exe version");
            Console.WriteLine("");
            Console.WriteLine("For more information, type msbp.exe help <command>");
        }

        public static void PrintBackupUsage()
        {
            Console.WriteLine("To backup a database, the first parameter must be the database in brackets, and the last parameter must be the file.  The middle parameters can modify the data, for example compressing it.");
            Console.WriteLine("To backup to a standard *.bak file:");
            Console.WriteLine("\tmsbp.exe backup [model] file:///c:\\model.bak");
            Console.WriteLine("To compress the backup file using gzip:");
            Console.WriteLine("\tmsbp.exe backup [model] gzip file:///c:\\model.bak.gz");
            Console.WriteLine("");
            Console.WriteLine("For more information on the different pipline options, type msbp.exe listplugins");
        }

        public static void PrintRestoreUsage()
        {
            Console.WriteLine("To restore a database, the first parameter must be the file, and the last parameter must be the database in brackets.  The middle parameters can modify the data, for example uncompressing it.");
            Console.WriteLine("To restore to a standard *.bak file:");
            Console.WriteLine("\tmsbp.exe restore file:///c:\\model.bak [model]");
            Console.WriteLine("To compress the backup file using gzip:");
            Console.WriteLine("\tmsbp.exe restore file:///c:\\model.bak.gz gzip [model]");
            Console.WriteLine("");
            Console.WriteLine("For more information on the different pipline options, type msbp.exe listplugins");
        }

        public static void PrintVerifyOnlyUsage()
        {
            Console.WriteLine("To verify a backup set, the first parameter must be the file, and the last parameter must be the database in brackets.  The middle parameters can modify the data, for example uncompressing it.");
            Console.WriteLine("To verify a standard *.bak file:");
            Console.WriteLine("\tmsbp.exe restoreverifyonly file:///c:\\model.bak [model]");
            Console.WriteLine("To decompress  the backup file using gzip:");
            Console.WriteLine("\tmsbp.exe restoreverifyonly file:///c:\\model.bak.gz gzip [model]");
            Console.WriteLine("");
            Console.WriteLine("For more information on the different pipline options, type msbp.exe listplugins");
        }

        public static void PrintRestoreHeaderOnlyUsage()
        {
            Console.WriteLine("To restore header only, the first parameter must be the metadata (hfl) file");
            Console.WriteLine("\tmsbp.exe restoreheaderonly file:///c:\\model.bak.gz.hfl");
        }

        public static void PrintRestoreFilelistOnlyUsage()
        {
            Console.WriteLine("To restore filelist only, the first parameter must be the metadata (hfl) file");
            Console.WriteLine("\tmsbp.exe restorefilelist file:///c:\\model.bak.gz.hfl");
        }

        public static void PrintPlugins(Dictionary<string, Type> pipelineComponents, Dictionary<string, Type> databaseComponents, Dictionary<string, Type> storageComponents)
        {
            Console.WriteLine("Database plugins:");
            PrintComponents(databaseComponents);
            Console.WriteLine("Pipeline plugins:");
            PrintComponents(pipelineComponents);
            Console.WriteLine("Storage plugins:");
            PrintComponents(storageComponents);

            Console.WriteLine("");
            Console.WriteLine("To find more information about a plugin, type msbp.exe helpplugin <plugin>");
        }

        public static void PrintComponents(Dictionary<string, Type> components)
        {
            foreach (var db in (from key in components.Keys select components[key].GetConstructor(new Type[0]) into constructorInfo where constructorInfo != null select constructorInfo.Invoke(new object[0])).OfType<IBackupPlugin>())
            {
                Console.WriteLine("\t" + db.Name);
            }
        }

        public static int PrintPluginHelp(string pluginName, IDictionary<string, Type> pipelineComponents, IDictionary<string, Type> databaseComponents, IDictionary<string, Type> storageComponents)
        {
            PrintPluginHelp(pluginName, databaseComponents);
            PrintPluginHelp(pluginName, pipelineComponents);
            PrintPluginHelp(pluginName, storageComponents);
            return 0;
        }

        public static void PrintPluginHelp(string pluginName, IDictionary<string, Type> components)
        {
            if (!components.ContainsKey(pluginName)) return;
            var constructorInfo = components[pluginName].GetConstructor(new Type[0]);
            if (constructorInfo == null) return;
            var db = constructorInfo.Invoke(new object[0]) as IBackupPlugin;
            if (db != null) Console.WriteLine(db.CommandLineHelp);
        }
    }
}