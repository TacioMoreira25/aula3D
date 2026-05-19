using System;
using System.IO;

namespace Aula3D.VisionCore.Utils
{
    public static class PathResolver
    {
        public static string ObterCaminhoTrackerService()
        {
            var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
            while (dir != null)
            {
                var target = Path.Combine(dir.FullName, "Aula3D.TrackerService");
                if (Directory.Exists(target))
                    return target;
                dir = dir.Parent;
            }
            
            dir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            while (dir != null)
            {
                var target = Path.Combine(dir.FullName, "Aula3D.TrackerService");
                if (Directory.Exists(target))
                    return target;
                dir = dir.Parent;
            }
            
            // Fallback para manter o comportamento exato de antes, caso não encontre subindo as pastas
            return Path.GetFullPath("Aula3D.TrackerService");
        }
    }
}
