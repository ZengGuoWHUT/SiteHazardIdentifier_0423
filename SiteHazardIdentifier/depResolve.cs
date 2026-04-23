using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace SiteHazardIdentifier
{
    public class DependencyResolver
    {
        private static bool _initialized = false;

        public static void Initialize()
        {
            if (_initialized) return;

            AppDomain.CurrentDomain.AssemblyResolve += OnAssemblyResolve;

            // 预加载必要的程序集
            PreloadAssemblies();

            _initialized = true;
        }

        private static Assembly OnAssemblyResolve(object sender, ResolveEventArgs args)
        {
            try
            {
                string assemblyName = new AssemblyName(args.Name).Name;
                Console.WriteLine($"尝试解析程序集: {assemblyName}");

                // 1. 首先检查是否已经加载
                var loadedAssemblies = AppDomain.CurrentDomain.GetAssemblies();
                foreach (var assembly in loadedAssemblies)
                {
                    if (assembly.GetName().Name == assemblyName)
                    {
                        Console.WriteLine($"已找到已加载的程序集: {assemblyName}");
                        return assembly;
                    }
                }

                // 2. 尝试从插件目录加载
                string pluginDir = Path.GetDirectoryName(
                    Assembly.GetExecutingAssembly().Location);

                string[] searchPaths =
                {
                    Path.Combine(pluginDir, assemblyName + ".dll"),
                    Path.Combine(pluginDir, "Lib", assemblyName + ".dll"),
                    Path.Combine(pluginDir, "Dependencies", assemblyName + ".dll"),
                    Path.Combine(Environment.GetFolderPath(
                        Environment.SpecialFolder.Windows),
                        "Microsoft.NET", "Framework", "v4.0.30319",
                        assemblyName + ".dll")
                };

                foreach (string path in searchPaths)
                {
                    if (File.Exists(path))
                    {
                        Console.WriteLine($"从路径加载程序集: {path}");
                        return Assembly.LoadFrom(path);
                    }
                }

                // 3. 对于Windows Forms核心程序集，使用Assembly.Load
                if (assemblyName.StartsWith("System.Windows.Forms") ||
                    assemblyName.StartsWith("System.Drawing") ||
                    assemblyName.StartsWith("Accessibility"))
                {
                    try
                    {
                        return Assembly.Load(assemblyName);
                    }
                    catch { }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"解析程序集时出错: {ex.Message}");
            }

            return null;
        }

        private static void PreloadAssemblies()
        {
            // 显式加载Windows Forms核心程序集
            string[] assembliesToPreload =
            {
                "System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089",
                "System.Drawing, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a",
                "System.Configuration, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a",
                "System.Core, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089"
            };

            foreach (string assemblyName in assembliesToPreload)
            {
                try
                {
                    Assembly.Load(assemblyName);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"预加载程序集失败 {assemblyName}: {ex.Message}");
                }
            }
        }
    }
}
